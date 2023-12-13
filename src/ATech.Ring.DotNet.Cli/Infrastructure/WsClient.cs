namespace ATech.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Logging;
using Queil.Ring.Protocol;
using Microsoft.Extensions.Logging;

public delegate Task<Ack> Dispatch(Message m, CancellationToken t);

public sealed class WsClient : IAsyncDisposable
{
    public Guid Id { get; }
    private readonly ILogger<WebsocketsHandler> _logger;
    private WebSocket Ws { get; }
    private Task _backgroundAwaiter = Task.CompletedTask;
    private readonly CancellationTokenSource _localCts = new();
    private readonly Channel<Task<Ack>> _channel = Channel.CreateUnbounded<Task<Ack>>();

    public WsClient(ILogger<WebsocketsHandler> logger, Guid id, WebSocket ws)
    {
        _logger = logger;
        Id = id;
        Ws = ws;
    }

    public bool IsOpen => Ws.State == WebSocketState.Open;

    public Task SendAsync(Message m)
    {
        try
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return Ws.SendMessageAsync(m);
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
            using (_logger.WithSentScope(isDelivered: false, type))
            {
                _logger.LogDebug("{Payload:l} {Id} ({TaskId})", payloadForLogging, Id, task.Id);
            }

            task.ContinueWith(_ =>
            {
                using (_logger.WithSentScope(isDelivered: true, type))
                {
                    _logger.LogDebug("{Payload:l} {Id} ({TaskId})", payloadForLogging, Id, task.Id);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
            return task;
        }
        catch (WebSocketException wse)
        {
            _logger.LogDebug("Exception {exception}", wse);
            return Task.CompletedTask;
        }
    }

    private async Task AckLongRunning(CancellationToken token)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(token))
            {
                while (_channel.Reader.TryPeek(out var peek))
                {
                    if (peek.IsCompleted)
                    {
                        if (!_channel.Reader.TryRead(out var task)) continue;

                        var ack = await task;
                        if (!_logger.IsEnabled(LogLevel.Debug)) await Ws.SendAckAsync(ack, token);
                        else
                        {
                            var sendTask = Ws.SendAckAsync(ack, token);
                            using (_logger.WithSentScope(isDelivered: false, M.ACK))
                            {
                                _logger.LogDebug("{Payload} {Id} ({TaskId})", ack, Id, task.Id);
                            }

                            await sendTask.ContinueWith(_ =>
                            {
                                using (_logger.WithSentScope(isDelivered: true, M.ACK))
                                {
                                    _logger.LogDebug("{Payload} {Id} ({TaskId})", ack, Id,
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
            using (_logger.WithClientScope()) _logger.LogInformation("Client disconnected ({Id}) ({WebSocketState})", Id, Ws.State);
        }
        catch (OperationCanceledException)
        {
            using (_logger.WithClientScope()) _logger.LogInformation("Client disconnected ({Id}) ({WebSocketState})", Id, Ws.State);
        }
        catch (WebSocketException wx)
        {
            using var _ = _logger.WithClientScope();
            _logger.LogInformation("Client {Id} aborted the connection.", Id);
            _logger.LogDebug(wx, "Exception details");
        }
        finally
        {
            using var _ = _logger.WithClientScope();
            _logger.LogDebug("Closing websocket ({Id}) ({WebSocketState})", Id, Ws.State);
            await Ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, default);
            _logger.LogDebug("Closed websocket ({Id}) ({WebSocketState})", Id, Ws.State);
        }

        return;

        Task<Ack>? YieldOrQueueLongRunning(ref Message message, CancellationToken token)
        {
            try
            {
                var (type, payload) = message;
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    using (_logger.WithReceivedScope(type))
                    {
                        _logger.LogDebug("{Payload}", payload.AsUtf8String());
                    }
                }

                var backgroundTask = dispatch(message, token);
                if (backgroundTask.IsCompleted) return backgroundTask;
                _channel.Writer.TryWrite(backgroundTask);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Listener terminating");
                _channel.Writer.TryWrite(Task.FromResult(Ack.Terminating));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server error");
                _channel.Writer.TryWrite(Task.FromResult(Ack.ServerError));
            }

            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _localCts.Cancel();
            await _backgroundAwaiter;
            Ws.Dispose();
        }
        catch (WebSocketException wx)
        {
            _logger.LogDebug(wx, "Error on disposing WsClient");
        }
    }
}