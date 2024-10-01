namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Configuration;
using Dtos;
using Tools;

internal sealed class RunnableContainer : IAsyncDisposable
{
    private readonly CancellationTokenSource _aggregateCts;
    private readonly IRunnableConfig _config;
    private readonly CancellationTokenSource _cts = new();

    private RunnableContainer(IRunnable runnable, IRunnableConfig config,
        CancellationToken token)
    {
        _config = config;
        Runnable = runnable;
        _aggregateCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
    }

    public IRunnable Runnable { get; }
    private Task? Task { get; set; }

    public async ValueTask DisposeAsync()
    {
        await CancelAsync();
        _aggregateCts.Dispose();
        _cts.Dispose();
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

    public Task ConfigureAsync() => Runnable.ConfigureAsync(_aggregateCts.Token);

    public void Start()
    {
        Task = Runnable.RunAsync(_aggregateCts.Token);
    }

    private async Task CancelAsync()
    {
        await _cts.CancelAsync();
        if (Task is { } t) await t;
        await Runnable.TerminateAsync();
    }

    public (Func<ProcessRunner, CancellationToken, Task<ExecuteTaskResult>>, bool bringDown) PrepareTask(string taskId)
    {
        if (_config.Tasks.TryGetValue(taskId, out var taskDefinition))
            return (async (runner, token) =>
            {
                var workDir =
                    Runnable.Details.TryGetValue(DetailsKeys.WorkDir, out var workingDir) && workingDir is string dir
                        ? dir
                        : null;
                runner.Command = taskDefinition.Command;
                var result = await runner.RunAsync(taskDefinition.Args, workDir, token: token);
                var finalResult = result.Task is { } t ? await t : result;
                return finalResult.IsSuccess ? ExecuteTaskResult.Ok : ExecuteTaskResult.Failed;
            }, bringDown: taskDefinition.BringDown);

        return (delegate { return Task.FromResult(ExecuteTaskResult.UnknownTask); }, bringDown: false);
    }
}
