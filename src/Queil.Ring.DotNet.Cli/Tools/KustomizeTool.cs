using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Abstractions.Tools;

namespace Queil.Ring.DotNet.Cli.Tools;

public class KustomizeTool(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; } = "kustomize";
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();
    public ILogger<ITool> Logger { get; } = logger;

    public async Task<ExecutionInfo> BuildAsync(string kustomizeDir, string outputFilePath, CancellationToken token)
    {
        var output = await this.RunProcessWaitAsync(["build", kustomizeDir], token);
        await File.WriteAllTextAsync(outputFilePath, output.Output, token);
        return output;
    }
}
