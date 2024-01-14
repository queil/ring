namespace Queil.Ring.DotNet.Cli.Workspace;

using System.Threading;
using System.Threading.Tasks;

public interface IWorkspaceInitHook
{
    Task RunAsync(CancellationToken token);
}
