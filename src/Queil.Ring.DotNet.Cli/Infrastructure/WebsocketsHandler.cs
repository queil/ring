namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Protocol;
using Protocol.Events;

public class WebsocketsHandler(
    IHostApplicationLifetime appLifetime,
    IReceiver queue,
    IServer server,
    ILogger<WebsocketsHandler> logger)
{
    private readonly ConcurrentDictionary<Guid, WsClient> _clients = new();

    private Task BroadcastAsync(Message m)
    {
        var tasks = new List<Task>();

        foreach (var client in _clients.Values.Where(x => x.IsOpen)) tasks.Add(client.SendAsync(m));

        return Task.WhenAll([.. tasks]);
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        try
        {
            using var _ = logger.WithClientScope();
            await server.InitializeAsync(token);

            var messageLoop = Task.Run(async () =>
            {
                while (await queue.WaitToReadAsync(appLifetime.ApplicationStopped))
                    try
                    {
                        await queue.DequeueAsync(BroadcastAsync);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
            }, appLifetime.ApplicationStopping);

            appLifetime.ApplicationStopping.Register(async () =>
            {
                using var _ = logger.WithHostScope(LogEvent.DESTROY);
                await server.TerminateAsync(default);
                logger.LogInformation("Workspace terminated");
                logger.LogDebug("Draining pub-sub");
                await queue.CompleteAsync(TimeSpan.FromSeconds(5));
                await messageLoop;
                logger.LogDebug("Shutdown");
            }, true);
            await messageLoop;
        }
        catch (OperationCanceledException)
        {
            using var _ = logger.WithHostScope(LogEvent.DESTROY);
            logger.LogInformation("Shutting down");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Critical error");
        }
    }

    public async Task ListenAsync(Guid clientId, Func<Task<WebSocket>> createSocket, CancellationToken t)
    {
        WsClient? client = null;
        client = CreateClient(clientId, await createSocket());
        await server.ConnectAsync(t);
        await client.ListenAsync(Dispatch, t);
        _clients.TryRemove(clientId, out _);
    }

    private Task<Ack> Dispatch(Message m, CancellationToken token)
    {
        Task<Ack> Dispatch(Message m)
        {
            return m switch
            {
                (M.LOAD, var path) => server.LoadAsync(path.AsUtf8String(), token),
                (M.UNLOAD, _) => server.UnloadAsync(token),
                (M.TERMINATE, _) => server.TerminateAsync(token),
                (M.START, _) => server.StartAsync(token),
                (M.STOP, _) => server.StopAsync(token),
                (M.RUNNABLE_INCLUDE, var runnableId) => server.IncludeAsync(runnableId.AsUtf8String(), token),
                (M.RUNNABLE_EXCLUDE, var runnableId) => server.ExcludeAsync(runnableId.AsUtf8String(), token),
                (M.RUNNABLE_EXECUTE_TASK, var taskInfo) => server.ExecuteTaskAsync(
                    RunnableTask.Deserialize(taskInfo) ?? throw new NullReferenceException("Runnable task is null"),
                    token),
                (M.WORKSPACE_APPLY_FLAVOUR, var flavour) => server.ApplyFlavourAsync(flavour.AsUtf8String(), token),
                (M.WORKSPACE_INFO_RQ, _) => Task.FromResult(server.RequestWorkspaceInfo()),
                (M.PING, _) => Task.FromResult(Ack.Alive),
                _ => Task.FromResult(Ack.NotSupported)
            };
        }

        return Dispatch(m);
    }

    public WsClient CreateClient(Guid key, WebSocket socket)
    {
        var wsClient = new WsClient(logger, key, socket);
        if (!_clients.TryAdd(key, wsClient)) throw new InvalidOperationException($"Client already exists: {key}");

        appLifetime.ApplicationStopped.Register(async () => await wsClient.DisposeAsync());

        return wsClient;
    }
}
