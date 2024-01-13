using Queil.Ring.Configuration;
using Queil.Ring.DotNet.Cli.Dtos;
using Queil.Ring.DotNet.Cli.Tools;

namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

internal sealed class RunnableContainer : IAsyncDisposable
{
    public IRunnable Runnable { get; }
    private readonly CancellationTokenSource _aggregateCts;
    private readonly CancellationTokenSource _cts = new();
    private readonly IRunnableConfig _config;
    private Task? Task { get; set; }

    private RunnableContainer(IRunnable runnable, IRunnableConfig config,
        CancellationToken token)
    {
        _config = config;
        Runnable = runnable;
        _aggregateCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
    }

    private async Task InitialiseAsync(TimeSpan delay)
    {
        if (delay != TimeSpan.Zero) await Task.Delay(delay, _aggregateCts.Token);
    }

    public static async Task<RunnableContainer> CreateAsync(IRunnableConfig cfg,
        Func<IRunnableConfig, IRunnable> factory, TimeSpan delay,
        CancellationToken token)
    {
        var container = new RunnableContainer(factory(cfg), cfg, token);
        await container.InitialiseAsync(delay);
        return container;
    }

    public void Start() => Task = Runnable.RunAsync(_aggregateCts.Token);

    private async Task CancelAsync()
    {
        _cts.Cancel();
        if (Task is { } t) await t;
        await Runnable.TerminateAsync();
    }

    public (Func<ProcessRunner, CancellationToken, Task<ExecuteTaskResult>>, bool bringDown) PrepareTask(string taskId)
    {
        if (_config.Tasks.TryGetValue(taskId, out var taskDefinition))
        {
            return (async (runner, token) =>
            {
                var workDir =
                    Runnable.Details.TryGetValue(DetailsKeys.WorkDir, out var workingDir) && workingDir is string dir
                        ? dir
                        : null;
                runner.Command = taskDefinition.Command;
                var result = await runner.RunProcessAsync(workDir, args: taskDefinition.Args.ToArray(), token: token);
                var finalResult = result.Task is { } t ? await t : result;
                return finalResult.IsSuccess ? ExecuteTaskResult.Ok : ExecuteTaskResult.Failed;
            }, bringDown: taskDefinition.BringDown);
        }

        return (delegate { return Task.FromResult(ExecuteTaskResult.UnknownTask); }, bringDown: false);
    }

    public async ValueTask DisposeAsync()
    {
        await CancelAsync();
        _aggregateCts.Dispose();
        _cts.Dispose();
    }
}