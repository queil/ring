﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.Configuration;
using Queil.Ring.DotNet.Cli.Dtos;
using Queil.Ring.Protocol.Events;

namespace Queil.Ring.DotNet.Cli.Workspace;

public interface IWorkspaceLauncher
{
    Task LoadAsync(ConfiguratorPaths paths, CancellationToken token);
    Task StartAsync(CancellationToken token);
    Task StopAsync(CancellationToken token);
    Task WaitUntilStoppedAsync(CancellationToken token);
    Task UnloadAsync(CancellationToken token);
    Task<ExcludeResult> ExcludeAsync(string id, CancellationToken token);
    Task<IncludeResult> IncludeAsync(string id, CancellationToken token);
    Task<ApplyFlavourResult> ApplyFlavourAsync(string flavour, CancellationToken token);
    string WorkspacePath { get; }
    void PublishStatus(ServerState serverState);
    Task<ExecuteTaskResult> ExecuteTaskAsync(RunnableTask task, CancellationToken token);
    event EventHandler OnInitiated;
}