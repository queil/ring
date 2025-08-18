namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Dtos;
using Logging;
using Microsoft.Extensions.Logging;
using Protocol;
using Protocol.Events;
using Stateless;
using Workspace;
using Scope = LightInject.Scope;
using State = Server.State;
using Trigger = Server.Trigger;

public class Server(
    Func<Scope> getScope,
    ILogger<Server> logger,
    IWorkspaceLauncher launcher,
    ISender sender)
    : IServer
{
    private readonly ServerFsm _fsm = new();
    private Scope? _scope;

    public Task InitializeAsync(CancellationToken token)
    {
        _fsm.Configure(State.Idle)
            .OnEntryFromAsync(Trigger.Unload, async () =>
            {
                await launcher.UnloadAsync(token);
                RequestWorkspaceInfo();
            })
            .Ignore(Trigger.Unload)
            .Ignore(Trigger.Stop)
            .Permit(Trigger.Load, State.Loaded);

        _fsm.Configure(State.Loaded)
            .OnEntryFromAsync(Trigger.Load.Of<string>(), async path =>
            {
                await launcher.LoadAsync(new ConfiguratorPaths { WorkspacePath = path }, token);
                RequestWorkspaceInfo();
            })
            .OnEntryFromAsync(Trigger.Stop, async () =>
            {
                await launcher.StopAsync(token);
                RequestWorkspaceInfo();
            })
            .InternalTransition(Trigger.Include, () => { })
            .InternalTransition(Trigger.Exclude, () => { })
            .Permit(Trigger.Unload, State.Idle)
            .Permit(Trigger.Start, State.Running)
            .Ignore(Trigger.Stop);

        _fsm.Configure(State.Running)
            .OnEntryFromAsync(Trigger.Start, async () =>
            {
                _scope = getScope();
                await launcher.StartAsync(token);
            })
            .InternalTransition(Trigger.Include, () => { })
            .InternalTransition(Trigger.Exclude, () => { })
            .Permit(Trigger.Stop, State.Loaded);

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
                State.Idle => Message.ServerIdle(),
                State.Loaded => Message.ServerLoaded(launcher.WorkspacePath.AsSpan()),
                State.Running => Message.ServerRunning(launcher.WorkspacePath.AsSpan()),
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
        await _fsm.FireAsync(Trigger.Load.Of<string>(), path);
        return Ack.Ok;
    }

    public async Task<Ack> UnloadAsync(CancellationToken token)
    {
        if (_fsm.CanFire(Trigger.Stop)) await _fsm.FireAsync(Trigger.Stop);
        if (_fsm.CanFire(Trigger.Unload)) await _fsm.FireAsync(Trigger.Unload);
        return Ack.Ok;
    }

    public async Task<Ack> TerminateAsync(CancellationToken token)
    {
        using var _ = logger.WithHostScope(LogEvent.DESTROY);
        logger.LogInformation("Shutdown requested");
        await _fsm.FireAsync(Trigger.Stop);
        await launcher.WaitUntilStoppedAsync(token);
        await _fsm.FireAsync(Trigger.Unload);
        _scope?.Dispose();
        await sender.EnqueueAsync(new Message(M.SERVER_SHUTDOWN), token);
        return Ack.Ok;
    }

    public async Task<Ack> IncludeAsync(string id, CancellationToken token)
    {
        await _fsm.FireAsync(Trigger.Include);
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
            State.Idle => ServerState.IDLE,
            State.Loaded => ServerState.LOADED,
            State.Running => ServerState.RUNNING,
            _ => throw new NotSupportedException($"State {_fsm.State} not supported")
        });

        return Ack.Ok;
    }

    public async Task<Ack> ExcludeAsync(string id, CancellationToken token)
    {
        await _fsm.FireAsync(Trigger.Exclude);
        return await launcher.ExcludeAsync(id, token) == ExcludeResult.UnknownRunnable ? Ack.NotFound : Ack.Ok;
    }

    public async Task<Ack> StartAsync(CancellationToken token)
    {
        await _fsm.FireAsync(Trigger.Start);
        return Ack.Ok;
    }

    public async Task<Ack> StopAsync(CancellationToken token)
    {
        await _fsm.FireAsync(Trigger.Stop);
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

    private class ServerFsm() : StateMachine<State, Trigger>(State.Idle);
}

internal static class StateMachineExtensions
{
    internal static StateMachine<State, Trigger>.TriggerWithParameters<T> Of<T>(this Trigger t) => new(t);
}
