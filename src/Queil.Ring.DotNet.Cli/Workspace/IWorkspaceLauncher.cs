namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Dtos;
using Protocol.Events;

public interface IWorkspaceLauncher
{
    string WorkspacePath { get; }
    Task LoadAsync(ConfiguratorPaths paths, CancellationToken token);
    Task StartAsync(CancellationToken token);
    Task StopAsync(CancellationToken token);
    Task WaitUntilStoppedAsync(CancellationToken token);
    Task UnloadAsync(CancellationToken token);
    Task<ExcludeResult> ExcludeAsync(string id, CancellationToken token);
    Task<IncludeResult> IncludeAsync(string id, CancellationToken token);
    Task<ApplyFlavourResult> ApplyFlavourAsync(string flavour, CancellationToken token);
    void PublishStatus(ServerState serverState);
    Task<ExecuteTaskResult> ExecuteTaskAsync(RunnableTask task, CancellationToken token);
    event EventHandler OnInitiated;
}
