namespace Queil.Ring.DotNet.Cli.Abstractions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Context;
using Dtos;
using Infrastructure;
using Logging;
using Microsoft.Extensions.Logging;
using Protocol;
using Stateless;

[DebuggerDisplay("{UniqueId}")]
public abstract class Runnable<TContext, TConfig> : IRunnable
    where TConfig : IRunnableConfig
{
    private readonly Dictionary<string, object> _details = [];
    private readonly Fsm _fsm = new();
    private readonly ILogger<Runnable<TContext, TConfig>> _logger;
    protected readonly ISender Sender;
    private TContext? _context;
    private Task _destroyTask = Task.CompletedTask;
    private Task _stopTask = Task.CompletedTask;

    protected Runnable(TConfig config, ILogger<Runnable<TContext, TConfig>> logger, ISender sender)
    {
        Config = config;
        if (Config.FriendlyName != null) _details.Add(DetailsKeys.FriendlyName, Config.FriendlyName);
        _logger = logger;
        Sender = sender;
    }

    protected virtual TimeSpan HealthCheckPeriod { get; } = TimeSpan.FromSeconds(5);
    protected virtual int MaxConsecutiveFailuresUntilDead { get; } = 2;
    protected virtual int MaxTotalFailuresUntilDead { get; } = 3;
    public TConfig Config { get; }
    public abstract string UniqueId { get; }
    public State State => _fsm.State;
    public event EventHandler? OnHealthCheckCompleted;
    public event EventHandler? OnInitExecuted;
    public IReadOnlyDictionary<string, object> Details => _details;

    public async Task ConfigureAsync(CancellationToken token)
    {
        using var _ = _logger.BeginScope(this.ToScope());
        var fsm = await InitFsm(token);
        await fsm.FireAsync(Trigger.Init);
    }

    public async Task RunAsync(CancellationToken token)
    {
        using var _ = _logger.BeginScope(this.ToScope());
        await _fsm.FireAsync(Trigger.Start);
    }

    public async Task TerminateAsync()
    {
        using var _ = _logger.BeginScope(this.ToScope());
        await _fsm.FireAsync(Trigger.Stop);
        await _stopTask;
        await _fsm.FireAsync(Trigger.Destroy);
        await _destroyTask;
    }

    /// <summary>
    ///     Details added via this method are pushed to clients where can be used for different purposes
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    protected void AddDetail(string key, object value)
    {
        _details.TryAdd(key, value);
    }

    protected abstract Task<TContext> InitAsync(CancellationToken token);
    protected abstract Task StartAsync(TContext ctx, CancellationToken token);
    protected abstract Task<HealthStatus> CheckHealthAsync(TContext ctx, CancellationToken token);
    protected abstract Task StopAsync(TContext ctx, CancellationToken token);
    protected abstract Task DestroyAsync(TContext ctx, CancellationToken token);

    protected virtual async Task RecoverAsync(TContext ctx, CancellationToken token)
    {
        await _fsm.FireAsync(Trigger.Stop);
        await _fsm.FireAsync(Trigger.Start);
    }

    private async Task<Fsm> InitFsm(CancellationToken token)
    {
        _fsm.Configure(State.Zero)
            .OnEntryFromAsync(Trigger.Destroy, ctx => _destroyTask = DestroyCoreAsync(_context, token))
            .Ignore(Trigger.NoOp)
            .Ignore(Trigger.HcOk)
            .Ignore(Trigger.HcUnhealthy)
            .Ignore(Trigger.Stop)
            .Ignore(Trigger.Destroy)
            .Permit(Trigger.Init, State.Idle);

        _fsm.Configure(State.Idle)
            .OnEntryFromAsync(Trigger.Init, () => InitCoreAsync(token))
            .OnEntryFromAsync(Trigger.Stop, () => _stopTask = StopCoreAsync(_context, token))
            .Permit(Trigger.Start, State.Pending)
            .Permit(Trigger.InitFailure, State.Pending)
            .Permit(Trigger.Destroy, State.Zero)
            .Ignore(Trigger.HcUnhealthy)
            .Ignore(Trigger.NoOp)
            .Ignore(Trigger.HcOk)
            .Ignore(Trigger.Stop);

        _fsm.Configure(State.Pending)
            .OnEntryFromAsync(Trigger.Start,
                async () =>
                {
                    await StartCoreAsync(_context, token);
                    await QueueHealthCheckAsync(token);
                })
            .OnEntryFromAsync(Trigger.InitFailure, () => QueueHealthCheckAsync(token))
            .Permit(Trigger.HealthLoop, State.ProbingHealth)
            .Permit(Trigger.Stop, State.Idle);

        _fsm.Configure(State.ProbingHealth)
            .OnEntryFromAsync(Trigger.HealthLoop,
                async () =>
                {
                    await Sender.EnqueueAsync(Message.RunnableHealthCheck(UniqueId), token);
                    var healthResult = await CheckHealthCoreAsync(_context, token);
                    await _fsm.FireAsync(healthResult switch
                    {
                        HealthStatus.Dead => Trigger.HcDead,
                        HealthStatus.Unhealthy => Trigger.HcUnhealthy,
                        HealthStatus.Ok => Trigger.HcOk,
                        HealthStatus.Ignore => Trigger.NoOp,
                        _ => Trigger.Invalid
                    });
                    OnHealthCheckCompleted?.Invoke(this, EventArgs.Empty);
                })
            .Permit(Trigger.HcOk, State.Healthy)
            .Permit(Trigger.HcDead, State.Dead)
            .Permit(Trigger.HcUnhealthy, State.Recovering)
            .Permit(Trigger.Stop, State.Idle)
            .Ignore(Trigger.NoOp);

        _fsm.Configure(State.Healthy)
            .OnEntryFromAsync(Trigger.HcOk, async () =>
            {
                await Sender.EnqueueAsync(Message.RunnableHealthy(UniqueId), token);
                await QueueHealthCheckAsync(token);
            })
            .Permit(Trigger.HealthLoop, State.ProbingHealth)
            .Permit(Trigger.Stop, State.Idle);

        _fsm.Configure(State.Dead)
            .OnEntryFromAsync(Trigger.HcDead,
                async () => { await Sender.EnqueueAsync(Message.RunnableDead(UniqueId), token); })
            .Permit(Trigger.Stop, State.Idle);

        _fsm.Configure(State.Recovering)
            .OnEntryFromAsync(Trigger.HcUnhealthy,
                async () =>
                {
                    await Sender.EnqueueAsync(Message.RunnableRecovering(UniqueId), token);
                    await RecoverCoreAsync(_context, token);
                })
            .Permit(Trigger.Stop, State.Idle);

        _fsm.OnTransitioned(t =>
        {
            using var _ = _logger.WithScope(UniqueId, LogEvent.TRACE);
            _logger.LogDebug("{Source} -> {Trigger} -> {Destination}", t.Source, t.Trigger, t.Destination);
        });

        await _fsm.ActivateAsync();
        return _fsm;

        async Task QueueHealthCheckAsync(CancellationToken t)
        {
            using var _ = _logger.WithScope(UniqueId, LogEvent.HEALTH);
            try
            {
                if (t.IsCancellationRequested) return;
                var delay = Task.Delay(HealthCheckPeriod, t);
                await delay.ConfigureAwait(false);
                delay.Dispose();
                if (t.IsCancellationRequested) return;
                await _fsm.FireAsync(Trigger.HealthLoop);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Health check cancelled");
            }
        }
    }

    private async Task InitCoreAsync(CancellationToken token)
    {
        try
        {
            using var _ = _logger.BeginScope(Scope.Event(LogEvent.INIT));
            _logger.LogDebug(LogEventStatus.PENDING);
            _context = await InitAsync(token);
            _logger.LogContextDebug(_context);
            _logger.LogDebug(LogEventStatus.OK);
            await Sender.EnqueueAsync(Message.RunnableInitiated(UniqueId), token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialization failed");
            _context = (TContext)RuntimeHelpers
                .GetUninitializedObject(typeof(TContext));
            await _fsm.FireAsync(Trigger.InitFailure);
        }
        finally
        {
            OnInitExecuted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected async Task StartCoreAsync(TContext ctx, CancellationToken token)
    {
        using var _ = _logger.BeginScope(Scope.Event(LogEvent.START));
        _logger.LogDebug(LogEventStatus.PENDING);
        await StartAsync(ctx, token);
        _logger.LogContextDebug(ctx);
        _logger.LogInformation(LogEventStatus.OK);
        await Sender.EnqueueAsync(Message.RunnableStarted(UniqueId), token);
    }

    private async Task<HealthStatus> CheckHealthCoreAsync(TContext ctx, CancellationToken token)
    {
        try
        {
            using var _ = _logger.BeginScope(Scope.Event(LogEvent.HEALTH));
            if (token.IsCancellationRequested) return HealthStatus.Ignore;
            _logger.LogDebug(LogEventStatus.PENDING);
            HealthStatus result;
            try
            {
                result = await CheckHealthAsync(ctx, token);
                if (token.IsCancellationRequested) return HealthStatus.Ignore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected healthcheck error");
                result = HealthStatus.Unhealthy;
            }

            _logger.LogContextDebug(ctx);

            switch (result)
            {
                case HealthStatus.Unhealthy:
                    _logger.LogError("UNHEALTHY");
                    break;
                case HealthStatus.Ok:
                    _logger.LogDebug(LogEventStatus.OK);
                    break;
                case HealthStatus.Ignore:
                    break;
                case HealthStatus.Dead:
                    _logger.LogError("DEAD");
                    break;
                default:
                    throw new NotSupportedException($"Status '{result}' is not supported.");
            }

            if (ctx is not ITrackRetries t) return result;

            if (result == HealthStatus.Ok)
            {
                t.ConsecutiveFailures = 0;
                return result;
            }

            t.ConsecutiveFailures++;
            t.TotalFailures++;
            token.ThrowIfCancellationRequested();
            return t.ConsecutiveFailures < MaxConsecutiveFailuresUntilDead
                   && t.TotalFailures < MaxTotalFailuresUntilDead
                ? result
                : HealthStatus.Dead;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("HealthCheck cancelled");
            return HealthStatus.Ignore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HealthCheck exception");
            throw;
        }
    }

    private async Task RecoverCoreAsync(TContext ctx, CancellationToken token)
    {
        using var _ = _logger.BeginScope(Scope.Event(LogEvent.RECOVERY));
        _logger.LogDebug(LogEventStatus.PENDING);
        await RecoverAsync(ctx, token);
        _logger.LogContextDebug(ctx);
    }

    protected async Task StopCoreAsync(TContext ctx, CancellationToken token)
    {
        using var _ = _logger.BeginScope(Scope.Event(LogEvent.STOP));
        try
        {
            _logger.LogDebug(LogEventStatus.PENDING);
            await StopAsync(ctx, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stopping runnable failed");
        }
        finally
        {
            await Sender.EnqueueAsync(Message.RunnableStopped(UniqueId), default);
            _logger.LogContextDebug(ctx);
            _logger.LogDebug(LogEventStatus.OK);
        }
    }

    private async Task DestroyCoreAsync(TContext ctx, CancellationToken token)
    {
        using var _ = _logger.BeginScope(Scope.Event(LogEvent.DESTROY));
        try
        {
            _logger.LogDebug(LogEventStatus.PENDING);
            await DestroyAsync(ctx, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Destroying runnable failed");
        }
        finally
        {
            await Sender.EnqueueAsync(Message.RunnableDestroyed(UniqueId), default);
            _logger.LogContextDebug(ctx);
            _logger.LogInformation(LogEventStatus.OK);
        }
    }

    private class Fsm() : StateMachine<State, Trigger>(State.Zero);
}

public enum State
{
    Zero,
    Idle,
    Pending,
    ProbingHealth,
    Healthy,
    Recovering,
    Dead
}

public enum Trigger
{
    NoOp,
    Invalid,
    Init,
    InitFailure,
    Start,
    Stop,
    Destroy,
    HealthLoop,
    HcUnhealthy,
    HcOk,
    HcDead
}
