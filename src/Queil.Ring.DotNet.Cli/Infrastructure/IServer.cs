﻿namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using Protocol;
using Protocol.Events;

public interface IServer
{
    Task InitializeAsync(CancellationToken token);
    Task<Ack> ConnectAsync(CancellationToken token);
    Task<Ack> LoadAsync(string path, CancellationToken token);
    Task<Ack> StartAsync(CancellationToken token);
    Task<Ack> StopAsync(CancellationToken token);
    Task<Ack> TerminateAsync(CancellationToken token);
    Task<Ack> UnloadAsync(CancellationToken token);
    Task<Ack> ExcludeAsync(string id, CancellationToken token);
    Task<Ack> IncludeAsync(string id, CancellationToken token);
    Task<Ack> ApplyFlavourAsync(string flavour, CancellationToken token);
    Task<Ack> ExecuteTaskAsync(RunnableTask task, CancellationToken token);
    Ack RequestWorkspaceInfo();
}
