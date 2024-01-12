using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Queil.Ring.Protocol;

public static class WebSocketExtensions
{
    public static async Task SendAckAsync(this WebSocket s, Ack status, CancellationToken token = default)
    {
        if (s.State != WebSocketState.Open) return;
        await s.SendAsync(new ArraySegment<byte>(new Message(M.ACK, (byte)status).Bytes.ToArray()),
            WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
    }

    public static Task SendMessageAsync(this WebSocket s, Message m, CancellationToken token = default)
    {
        return s.State != WebSocketState.Open
            ? Task.CompletedTask
            : s.SendAsync(new ArraySegment<byte>(m.Bytes.SliceUntilNull().ToArray()), WebSocketMessageType.Binary, true,
                token);
    }

    public static async Task ListenAsync(this WebSocket webSocket, HandleMessage onReceived,
        CancellationToken token = default)
    {
        WebSocketReceiveResult? result;
        do
        {
            using var ms = new MemoryStream(Constants.MaxMessageSize);
            var buffer = ArrayPool<byte>.Shared.Rent(Constants.MaxMessageSize);
            try
            {
                do
                {
                    Array.Clear(buffer);
                    result = await webSocket.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    await ms.WriteAsync(buffer.AsMemory(0, result.Count), token);

                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                await ms.FlushAsync(token);
                var maybeAck = OnReceived(ms.ToArray(), onReceived, token);
                if (maybeAck is Task<Ack> ack)
                {
                    await webSocket.SendAckAsync(await ack, token);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        } while (!result.CloseStatus.HasValue && !token.IsCancellationRequested);

        return;

        static Task? OnReceived(ReadOnlySpan<byte> buffer, HandleMessage onReceived, CancellationToken token)
        {
            var m = new Message(buffer.SliceUntilNull());
            return onReceived(ref m, token);
        }
    }

    public delegate Task? HandleMessage(ref Message message, CancellationToken token);
}