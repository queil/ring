namespace Queil.Ring.DotNet.Cli.Tools;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class KustomizeTool(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; } = "kustomize";
    public string[] DefaultArgs { get; set; } = [];
    public ILogger<ITool> Logger { get; } = logger;

    public async Task<ExecutionInfo> BuildAsync(string kustomizeDir, string outputFilePath, CancellationToken token)
    {
        var output = await this.RunAsync(["build", kustomizeDir], foreground: true, token: token);
        await File.WriteAllTextAsync(outputFilePath, output.Output, token);
        return output;
    }
}
