namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Protocol;

public delegate Task OnDequeue(Message message);

public interface IReceiver
{
    Task DequeueAsync(OnDequeue action);
    Task CompleteAsync(TimeSpan timeout);
    Task<bool> WaitToReadAsync(CancellationToken token);
}
