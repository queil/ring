using System.Threading;
using System.Threading.Tasks;

namespace Queil.Ring.DotNet.Cli.Workspace;

public interface IWorkspaceInitHook
{
    Task RunAsync(CancellationToken token);
}