namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Protocol;

public sealed class Queue() : ISender, IReceiver, IDisposable
{
    private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>();

    public void Complete()
    {
        _channel.Writer.Complete();
        _channelCompleted.Cancel();
    }

    private readonly CancellationTokenSource _channelCompleted = new();

    public CancellationToken Completed => _channelCompleted.Token;

    public async Task<bool> WaitToReadAsync(CancellationToken token)
    {
        try
        {
            return await _channel.Reader.WaitToReadAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> DequeueAsync(OnDequeue action)
    {
        if (!_channel.Reader.TryRead(out var bytes)) return true;
        try
        {
            var waitForNext = new Message(bytes).Type != M.SERVER_SHUTDOWN;
            await action(new Message(bytes));
            return waitForNext;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes, true);
        }
    }

    public ValueTask EnqueueAsync(Message message, CancellationToken token) =>
        _channel.Writer.WriteAsync(CopyBytes(message), token);

    public void Enqueue(Message message)
    {
        _channel.Writer.TryWrite(CopyBytes(message));
    }

    private static byte[] CopyBytes(Message message)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(message.Bytes.Length);
        Array.Clear(bytes);
        message.Bytes.CopyTo(bytes);
        return bytes;
    }

    public void Dispose() => _channelCompleted.Dispose();
}
