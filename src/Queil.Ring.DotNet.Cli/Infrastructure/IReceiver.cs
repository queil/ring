namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using Protocol;

public delegate Task OnDequeue(Message message);

public interface IReceiver
{
    Task DequeueAsync(OnDequeue action);
    void Complete();
    Task<bool> WaitToReadAsync(CancellationToken token);
    CancellationToken Completed { get; }
}
