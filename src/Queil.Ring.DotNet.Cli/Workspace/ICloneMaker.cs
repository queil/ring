namespace Queil.Ring.DotNet.Cli.Workspace;

using System.Threading;
using System.Threading.Tasks;

public interface ICloneMaker
{
    Task CloneWorkspaceRepos(string workspacePath, string? outputDir = null, CancellationToken token = default);
}
