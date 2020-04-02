using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ATech.Ring.Configuration.Interfaces;
using ATech.Ring.DotNet.Cli.Abstractions.Context;
using ATech.Ring.DotNet.Cli.Logging;
using ATech.Ring.Protocol;
using ATech.Ring.Protocol.Events;
using Microsoft.Extensions.Logging;

namespace ATech.Ring.DotNet.Cli.Abstractions
{
    [DebuggerDisplay("{UniqueId}")]
    public abstract class Runnable<TContext, TConfig> : IRunnable, IRunnableIds
        where TConfig : IRunnableConfig
    {
        private readonly ILogger<Runnable<TContext, TConfig>> _logger;
        private readonly Fsm _fsm = new Fsm();
        private TContext _context { get; set; }
        protected readonly ISender<IRingEvent> Sender;
        protected virtual TimeSpan HealthCheckPeriod { get; } = TimeSpan.FromSeconds(5);
        protected virtual int MaxConsecutiveFailuresUntilDead { get; } = 2;
        protected virtual int MaxTotalFailuresUntilDead { get; } = 3;
        public TConfig Config { get; private set; }
        public abstract string UniqueId { get; }
        public State State => _fsm.State;
        protected readonly CancellationTokenSource CancellationSource = new CancellationTokenSource();

        public event EventHandler OnHealthCheckCompleted;
        public IReadOnlyDictionary<string, object> Details => _details;
        private readonly Dictionary<string, object> _details = new Dictionary<string, object>();
        /// <summary>
        /// Details added via this method are pushed to clients where can be used for different purposes
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected void AddDetail(string key, object value) => _details.TryAdd(key, value);

        protected Runnable(ILogger<Runnable<TContext, TConfig>> logger, ISender<IRingEvent> sender)
        {
            _logger = logger;
            Sender = sender;
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
                .OnEntryFromAsync(Trigger.Destroy, async ctx => await DestroyCoreAsync(_context, token))
                .Ignore(Trigger.NoOp)
                .Ignore(Trigger.HcOk)
                .Ignore(Trigger.HcUnhealthy)
                .Permit(Trigger.Init, State.Ready);

            _fsm.Configure(State.Ready)
                .OnEntryFromAsync(Trigger.Init, () => InitCoreAsync(token))
                .OnEntryFromAsync(Trigger.Stop, () => StopCoreAsync(_context, token))
                .Permit(Trigger.Start, State.Started)
                .Permit(Trigger.StartBypass, State.Started)
                .Permit(Trigger.Destroy, State.Zero)
                .Ignore(Trigger.HcUnhealthy)
                .Ignore(Trigger.NoOp)
                .Ignore(Trigger.HcOk)
                .Ignore(Trigger.Stop);

            _fsm.Configure(State.Started)
                .OnEntryFromAsync(Trigger.Start,
                    async () =>
                    {
                        await StartCoreAsync(_context, token);
                        await QueueHealthCheckAsync(token);
                    })
                .OnEntryFromAsync(Trigger.StartBypass, () => QueueHealthCheckAsync(token))
                .Permit(Trigger.StartHealthCheck, State.CheckingHealth)
                .Permit(Trigger.Stop, State.Ready);

            _fsm.Configure(State.CheckingHealth)
                .OnEntryFromAsync(Trigger.StartHealthCheck,
                    async () =>
                    {
                        Sender.Enqueue(RunnableEvent.New<RunnableHealthCheck>(this));
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
                .Permit(Trigger.Stop, State.Ready)
                .Ignore(Trigger.NoOp);

            _fsm.Configure(State.Healthy)
                .OnEntryFromAsync(Trigger.HcOk, async () =>
                {
                    Sender.Enqueue(RunnableEvent.New<RunnableHealthy>(this));
                    await QueueHealthCheckAsync(token);
                })
                .Permit(Trigger.StartHealthCheck, State.CheckingHealth)
                .Permit(Trigger.Stop, State.Ready);


            _fsm.Configure(State.Dead)
                .OnEntryFromAsync(Trigger.HcDead, () =>
                {
                    Sender.Enqueue(RunnableEvent.New<RunnableDead>(this));
                    return Task.CompletedTask;
                })
                .Permit(Trigger.Stop, State.Ready);

            _fsm.Configure(State.Recovering)
                .OnEntryFromAsync(Trigger.HcUnhealthy,
                    async () =>
                        {
                            Sender.Enqueue(RunnableEvent.New<RunnableRecovering>(this));
                            await RecoverCoreAsync(_context, token);
                        })
                .Permit(Trigger.Stop, State.Ready);

            _fsm.OnTransitioned(t => _logger.LogDebug($"{t.Source} -> {t.Trigger} -> {t.Destination}"));


            await _fsm.ActivateAsync();
            return _fsm;

            async Task QueueHealthCheckAsync(CancellationToken t)
            {
                try
                {
                    if (CancellationSource.IsCancellationRequested) return;
                    var delay = Task.Delay(HealthCheckPeriod, t);
                    await delay.ConfigureAwait(false);
                    delay.Dispose();
                    if (CancellationSource.IsCancellationRequested) return;
                    await _fsm.FireAsync(Trigger.StartHealthCheck);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Health check cancelled");
                }
            }
        }

        public async Task RunAsync(IRunnableConfig config, CancellationToken token)
        {
            Config = (TConfig)config;
            using var _ = _logger.BeginScope(this.ToScope());
            var fsm = await InitFsm(CancellationSource.Token);

            await fsm.FireAsync(Trigger.Init);
        }

        public async Task TerminateAsync(CancellationToken token)
        {
            using var _ = _logger.BeginScope(this.ToScope());
            CancellationSource.Cancel();
            await _fsm.FireAsync(Trigger.Stop);
            await _fsm.FireAsync(Trigger.Destroy);
        }

        private async Task InitCoreAsync(CancellationToken token)
        {
            try
            {
                using var _ = _logger.BeginScope(Scope.Phase(Phase.INIT));
                _logger.LogDebug(PhaseStatus.PENDING);
                _context = await InitAsync(token);
                _logger.LogContextDebug(_context);
                _logger.LogDebug(PhaseStatus.OK);
                Sender.Enqueue(RunnableEvent.New<RunnableInitiated>(this));
                await _fsm.FireAsync(Trigger.Start);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialization failed");
                _context = (TContext)FormatterServices.GetUninitializedObject(typeof(TContext));
                await _fsm.FireAsync(Trigger.StartBypass);
            }
        }
        protected async Task StartCoreAsync(TContext ctx, CancellationToken token)
        {
            using var _ = _logger.BeginScope(Scope.Phase(Phase.START));
            _logger.LogDebug(PhaseStatus.PENDING);
            await StartAsync(ctx, token);
            _logger.LogContextDebug(ctx);
            _logger.LogDebug(PhaseStatus.OK);
            Sender.Enqueue(RunnableEvent.New<RunnableStarted>(this));
        }
        private async Task<HealthStatus> CheckHealthCoreAsync(TContext ctx, CancellationToken token)
        {
            try
            {
                using var _ = _logger.BeginScope(Scope.Phase(Phase.HEALTH));
                if (CancellationSource.IsCancellationRequested) return HealthStatus.Ignore;
                _logger.LogDebug(PhaseStatus.PENDING);
                HealthStatus result;
                try
                {
                    result = await CheckHealthAsync(ctx, token);
                    if (CancellationSource.IsCancellationRequested) return HealthStatus.Ignore;
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
                        _logger.LogDebug(PhaseStatus.OK);
                        break;
                    case HealthStatus.Ignore:
                        break;
                    case HealthStatus.Dead:
                        _logger.LogError("DEAD");
                        break;
                    default:
                        throw new NotSupportedException($"Status '{result}' is not supported.");
                }

                if (!(ctx is ITrackRetries t)) return result;

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "HealthCheck exception");
                throw;
            }
        }

        private async Task RecoverCoreAsync(TContext ctx, CancellationToken token)
        {
            using var _ = _logger.BeginScope(Scope.Phase(Phase.RECOVERY));
            _logger.LogDebug(PhaseStatus.PENDING);
            await RecoverAsync(ctx, token);
            _logger.LogContextDebug(ctx);
        }

        protected async Task StopCoreAsync(TContext ctx, CancellationToken token)
        {
            using var _ = _logger.BeginScope(Scope.Phase(Phase.STOP));
            _logger.LogDebug(PhaseStatus.PENDING);
            await StopAsync(ctx, token);
            _logger.LogContextDebug(ctx);
            _logger.LogDebug(PhaseStatus.OK);
            Sender.Enqueue(RunnableEvent.New<RunnableStopped>(this));
        }
        private async Task DestroyCoreAsync(TContext ctx, CancellationToken token)
        {
            using var _ = _logger.BeginScope(Scope.Phase(Phase.DESTROY));
            _logger.LogDebug(PhaseStatus.PENDING);
            await DestroyAsync(ctx, token);
            _logger.LogContextDebug(ctx);
            _logger.LogInformation(PhaseStatus.OK);
            Sender.Enqueue(RunnableEvent.New<RunnableDestroyed>(this));
        }

        private class Fsm : Stateless.StateMachine<State, Trigger>
        {
            public Fsm() : base(State.Zero)
            {
            }
        }
    }

    public enum State { Zero, Ready, Started, CheckingHealth, Healthy, Recovering, Dead }
    public enum Trigger { NoOp, Invalid, Create, Init, StartBypass, Start, Stop, Destroy, StartHealthCheck, HcUnhealthy, HcOk, HcDead }
}