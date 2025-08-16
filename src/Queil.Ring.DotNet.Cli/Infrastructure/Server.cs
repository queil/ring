namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Dtos;
using Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Protocol;
using Protocol.Events;
using Stateless;
using Workspace;
using Scope = LightInject.Scope;
using S = Server.State;
using T = Server.Trigger;

public class Server(
    Func<Scope> getScope,
    ILogger<Server> logger,
    IWorkspaceLauncher launcher,
    IHostApplicationLifetime appLifetime,
    ISender sender)
    : IServer
{
    private readonly ServerFsm _fsm = new();
    private Scope? _scope;

    public Task InitializeAsync(CancellationToken token)
    {
        _fsm.Configure(S.Idle)
            .OnEntryFromAsync(T.Unload, async () =>
            {
                await launcher.UnloadAsync(token);
                RequestWorkspaceInfo();
            })
            .Ignore(T.Unload)
            .Ignore(T.Stop)
            .Permit(T.Load, S.Loaded);

        _fsm.Configure(S.Loaded)
            .OnEntryFromAsync(T.Load.Of<string>(), async path =>
            {
                await launcher.LoadAsync(new ConfiguratorPaths { WorkspacePath = path }, token);
                RequestWorkspaceInfo();
            })
            .OnEntryFromAsync(T.Stop, async () =>
            {
                await launcher.StopAsync(token);
                RequestWorkspaceInfo();
            })
            .InternalTransition(T.Include, () => { })
            .InternalTransition(T.Exclude, () => { })
            .Permit(T.Unload, S.Idle)
            .Permit(T.Start, S.Running)
            .Ignore(T.Stop);

        _fsm.Configure(S.Running)
            .OnEntryFromAsync(T.Start, async () =>
            {
                _scope = getScope();
                await launcher.StartAsync(token);
            })
            .InternalTransition(T.Include, () => { })
            .InternalTransition(T.Exclude, () => { })
            .Permit(T.Stop, S.Loaded);

        _fsm.OnUnhandledTrigger((s, t) =>
            logger.LogInformation("Trigger: {trigger} is not supported in state: {state}", t, s));
        return Task.CompletedTask;
    }

    public async Task<Ack> ConnectAsync(CancellationToken token)
    {
        ValueTask EnqueueServerStatusAsync()
        {
            var maybeMessage = _fsm.State switch
            {
                S.Idle => Message.ServerIdle(),
                S.Loaded => Message.ServerLoaded(launcher.WorkspacePath.AsSpan()),
                S.Running => Message.ServerRunning(launcher.WorkspacePath.AsSpan()),
                _ => Message.Empty()
            };
            return maybeMessage is not { Type: M.EMPTY }
                ? sender.EnqueueAsync(maybeMessage, token)
                : ValueTask.CompletedTask;
        }

        await EnqueueServerStatusAsync();
        return Ack.Ok;
    }

    public async Task<Ack> LoadAsync(string path, CancellationToken token)
    {
        await _fsm.FireAsync(T.Load.Of<string>(), path);
        return Ack.Ok;
    }

    public async Task<Ack> UnloadAsync(CancellationToken token)
    {
        if (_fsm.CanFire(T.Stop)) await _fsm.FireAsync(T.Stop);
        if (_fsm.CanFire(T.Unload)) await _fsm.FireAsync(T.Unload);
        return Ack.Ok;
    }

    public async Task<Ack> TerminateAsync(CancellationToken token)
    {
        using var _ = logger.WithHostScope(LogEvent.DESTROY);
        logger.LogInformation("Shutdown requested");
        await _fsm.FireAsync(T.Stop);
        await launcher.WaitUntilStoppedAsync(token);
        await _fsm.FireAsync(T.Unload);
        _scope?.Dispose();
        await sender.EnqueueAsync(new Message(M.SERVER_SHUTDOWN), token);
        return Ack.Ok;
    }

    public async Task<Ack> IncludeAsync(string id, CancellationToken token)
    {
        await _fsm.FireAsync(T.Include);
        return await launcher.IncludeAsync(id, token) == IncludeResult.UnknownRunnable ? Ack.NotFound : Ack.Ok;
    }

    public async Task<Ack> ApplyFlavourAsync(string flavour, CancellationToken token) =>
        await launcher.ApplyFlavourAsync(flavour, token) == ApplyFlavourResult.UnknownFlavour
            ? Ack.NotFound
            : Ack.Ok;

    public async Task<Ack> ExecuteTaskAsync(RunnableTask task, CancellationToken token)
    {
        return await launcher.ExecuteTaskAsync(task, token) switch
        {
            ExecuteTaskResult.UnknownRunnable or ExecuteTaskResult.UnknownTask => Ack.NotFound,
            ExecuteTaskResult.Failed => Ack.TaskFailed,
            _ => Ack.TaskOk
        };
    }

    public Ack RequestWorkspaceInfo()
    {
        launcher.PublishStatus(_fsm.State switch
        {
            S.Idle => ServerState.IDLE,
            S.Loaded => ServerState.LOADED,
            S.Running => ServerState.RUNNING,
            _ => throw new NotSupportedException($"State {_fsm.State} not supported")
        });

        return Ack.Ok;
    }

    public async Task<Ack> ExcludeAsync(string id, CancellationToken token)
    {
        await _fsm.FireAsync(T.Exclude);
        return await launcher.ExcludeAsync(id, token) == ExcludeResult.UnknownRunnable ? Ack.NotFound : Ack.Ok;
    }

    public async Task<Ack> StartAsync(CancellationToken token)
    {
        await _fsm.FireAsync(T.Start);
        return Ack.Ok;
    }

    public async Task<Ack> StopAsync(CancellationToken token)
    {
        await _fsm.FireAsync(T.Stop);
        return Ack.Ok;
    }

    internal enum State
    {
        Idle,
        Loaded,
        Running
    }

    internal enum Trigger
    {
        Load,
        Unload,
        Terminate,
        Include,
        Exclude,
        Start,
        Stop
    }

    internal class ServerFsm() : StateMachine<S, T>(S.Idle);
}

internal static class StateMachineExtensions
{
    internal static StateMachine<S, Server.Trigger>.TriggerWithParameters<T> Of<T>(this Server.Trigger t) => new(t);
}
