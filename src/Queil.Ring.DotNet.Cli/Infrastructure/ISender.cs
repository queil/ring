namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using Protocol;

public interface ISender
{
    ValueTask EnqueueAsync(Message message, CancellationToken token);
    void Enqueue(Message message);
}
