namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Logging;
using Microsoft.Extensions.Logging;
using Protocol;

public delegate Task<Ack> Dispatch(Message m, CancellationToken t);

public sealed class WsClient(ILogger<WebsocketsHandler> logger, Guid id, WebSocket ws) : IAsyncDisposable
{
    private readonly Channel<Task<Ack>> _channel = Channel.CreateUnbounded<Task<Ack>>();
    private readonly CancellationTokenSource _localCts = new();
    private Task _backgroundAwaiter = Task.CompletedTask;
    public Guid Id { get; } = id;
    private WebSocket Ws { get; } = ws;

    public bool IsOpen => Ws.State == WebSocketState.Open;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _localCts.CancelAsync();
            await _backgroundAwaiter;
            Ws.Dispose();
        }
        catch (WebSocketException wx)
        {
            logger.LogDebug(wx, "Error on disposing WsClient");
        }
    }

    public Task SendAsync(Message m)
    {
        try
        {
            if (!logger.IsEnabled(LogLevel.Debug)) return Ws.SendMessageAsync(m);
            var type = m.Type;
            string? payloadForLogging = null;

            try
            {
                payloadForLogging = Regex.Unescape(m.PayloadString);
            }
            catch
            {
                payloadForLogging = m.PayloadString;
            }

            var task = Ws.SendMessageAsync(m);
            using (logger.WithSentScope(false, type))
            {
                logger.LogDebug("{Payload:l} {Id} ({TaskId})", payloadForLogging, Id, task.Id);
            }

            task.ContinueWith(_ =>
            {
                using (logger.WithSentScope(true, type))
                {
                    logger.LogDebug("{Payload:l} {Id} ({TaskId})", payloadForLogging, Id, task.Id);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
            return task;
        }
        catch (WebSocketException wse)
        {
            logger.LogDebug("Exception {exception}", wse);
            return Task.CompletedTask;
        }
    }

    private async Task AckLongRunning(CancellationToken token)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(token))
                while (_channel.Reader.TryPeek(out var peek))
                    if (peek.IsCompleted)
                    {
                        if (!_channel.Reader.TryRead(out var task)) continue;

                        var ack = await task;
                        if (!logger.IsEnabled(LogLevel.Debug))
                        {
                            await Ws.SendAckAsync(ack, token);
                        }
                        else
                        {
                            var sendTask = Ws.SendAckAsync(ack, token);
                            using (logger.WithSentScope(false, M.ACK))
                            {
                                logger.LogDebug("{Payload} {Id} ({TaskId})", ack, Id, task.Id);
                            }

                            await sendTask.ContinueWith(_ =>
                            {
                                using (logger.WithSentScope(true, M.ACK))
                                {
                                    logger.LogDebug("{Payload} {Id} ({TaskId})", ack, Id,
                                        task.Id);
                                }
                            }, TaskContinuationOptions.OnlyOnRanToCompletion);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), token);
                    }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task ListenAsync(Dispatch dispatch, CancellationToken t)
    {
        try
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(t, _localCts.Token);
            _backgroundAwaiter = Task.Run(() => AckLongRunning(cts.Token), cts.Token);
            await Ws.ListenAsync(YieldOrQueueLongRunning, cts.Token);
            using (logger.WithClientScope())
            {
                logger.LogInformation("Client disconnected ({Id}) ({WebSocketState})", Id, Ws.State);
            }
        }
        catch (WebSocketException wx)
        {
            using var _ = logger.WithClientScope();
            logger.LogInformation("Client {Id} aborted the connection.", Id);
            logger.LogDebug(wx, "Exception details");
        }
        finally
        {
            using var _ = logger.WithClientScope();
            if (IsOpen)
            {
                logger.LogDebug("Closing websocket ({Id}) ({WebSocketState})", Id, Ws.State);
                await Ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, default);
                logger.LogDebug("Closed websocket ({Id}) ({WebSocketState})", Id, Ws.State);
            }
        }

        return;

        Task<Ack>? YieldOrQueueLongRunning(ref Message message, CancellationToken token)
        {
            try
            {
                var (type, payload) = message;
                if (logger.IsEnabled(LogLevel.Debug))
                    using (logger.WithReceivedScope(type))
                    {
                        logger.LogDebug("{Payload}", payload.AsUtf8String());
                    }

                var backgroundTask = dispatch(message, token);
                if (backgroundTask.IsCompleted) return backgroundTask;
                _channel.Writer.TryWrite(backgroundTask);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Listener terminating");
                _channel.Writer.TryWrite(Task.FromResult(Ack.Terminating));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Server error");
                _channel.Writer.TryWrite(Task.FromResult(Ack.ServerError));
            }

            return null;
        }
    }
}
