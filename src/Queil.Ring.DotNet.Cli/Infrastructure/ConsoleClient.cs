namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Cli;
using Logging;
using Microsoft.Extensions.Logging;
using Protocol;

public class ConsoleClient(ILogger<ConsoleClient> logger, ServeOptions options)
{
    private static readonly Guid ClientId = Guid.Parse("842fcc9e-c1bb-420d-b1e7-b3465aafa4e2");
    private ClientWebSocket? _clientSocket;
    private Task _clientTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken token)
    {
        if (options is not ConsoleOptions consoleOpts) return Task.CompletedTask;

        _clientTask = Task.Run(async () =>
        {
            _clientSocket = new ClientWebSocket();
            try
            {
                using (logger.WithHostScope(LogEvent.INIT))
                {
                    if (consoleOpts.StartupDelaySeconds > 0)
                        logger.LogDebug("Delaying startup by: {StartupDelaySeconds} seconds",
                            consoleOpts.StartupDelaySeconds);
                }

                await Task.Delay(TimeSpan.FromSeconds(consoleOpts.StartupDelaySeconds), token);
                await _clientSocket.ConnectAsync(new Uri($"ws://localhost:{options.Port}/ws?clientId={ClientId}"),
                    token);
                await _clientSocket.SendMessageAsync(new Message(M.LOAD, consoleOpts.WorkspacePath), token);
                await _clientSocket.SendMessageAsync(M.START, token);
            }
            catch (Exception ex)
            {
                logger.LogError("Exception: {exception}", ex);
            }
        }, token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken token)
    {
        try
        {
            if (options is not ConsoleOptions) return;
            await _clientTask;
            if (_clientSocket is { } s)
                await s.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "terminating", token);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Console client terminating");
        }
    }
}
