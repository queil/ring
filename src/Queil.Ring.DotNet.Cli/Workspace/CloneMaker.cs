namespace Queil.Ring.DotNet.Cli.Workspace;

using Configuration;
using Logging;
using Tools;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class CloneMaker(ILogger<CloneMaker> logger, IConfigurator configurator, GitClone gitClone)
    : ICloneMaker
{
    public async Task CloneWorkspaceRepos(string workspacePath, string? outputDir = null, CancellationToken token = default)
    {
        using var _ = logger.WithScope(nameof(CloneMaker), LogEvent.GIT);
        await configurator.LoadAsync(new ConfiguratorPaths { WorkspacePath = workspacePath }, token);
        var haveValidGitUrl = configurator.Current.Values.OfType<IFromGit>().ToLookup(x => !string.IsNullOrWhiteSpace(x.SshRepoUrl));

        foreach (var invalidCfg in haveValidGitUrl[false].Cast<IRunnableConfig>())
        {
            logger.LogInformation("{parameter} is not specified for {runnableId}. Skipping.", nameof(IFromGit.SshRepoUrl), invalidCfg.UniqueId);
        }

        foreach (var gitCfg in haveValidGitUrl[true].GroupBy(x => gitClone.ResolveFullClonePath(x, outputDir)).Select(x => x.First()))
        {
            var output = await gitClone.CloneOrPullAsync(gitCfg, token, rootPathOverride: outputDir);
            if (output.IsSuccess) continue;
            break;
        }
    }
}