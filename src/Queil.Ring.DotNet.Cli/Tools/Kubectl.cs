namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Infrastructure;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class KubectlBundle(
    ILogger<ITool> logger,
    Kubernetes client,
    KustomizeTool kustomize,
    IOptions<RingConfiguration> config)
    : ITool
{
    private readonly string[] _allowedContexts = config.Value.Kubernetes.AllowedContexts ?? Array.Empty<string>();

    public string Command { get; set; } = "kubectl";
    public string[] DefaultArgs { get; set; } = [];
    public ILogger<ITool> Logger { get; } = logger;

    public async Task EnsureContextIsAllowed(CancellationToken token)
    {
        var result = await this.RunAsync(["config", "current-context"], wait: true, token: token);
        var currentContext = result.Output;
        if (!_allowedContexts.Contains(currentContext))
            throw new InvalidOperationException(
                $"Kubernetes context '{currentContext}' is not allowed. Allowed contexts: {string.Join(", ", _allowedContexts)}");
    }

    public async Task<bool> IsValidManifestAsync(string filePath, CancellationToken token)
    {
        var result = await this.RunAsync([
            "apply", "--validate=true", "--dry-run=client", "-f", $"\"{filePath}\""
        ], wait: true, token: token);
        return result.IsSuccess;
    }

    public async Task<ExecutionInfo> KustomizeBuildAsync(string kustomizeDir, string outputFilePath,
        CancellationToken token) =>
        await kustomize.BuildAsync(kustomizeDir, outputFilePath, token);

    public async Task<ExecutionInfo> ApplyJsonPathAsync(string path, string jsonPath, CancellationToken token)
    {
        await EnsureContextIsAllowed(token);
        return await this.RunAsync(["apply", "-o", $"jsonpath=\"{jsonPath}\"", "-f", $"\"{path}\""],
            wait: true, token: token);
    }

    public async Task<string[]> GetPods(string nameSpace)
    {
        return (await client.ListNamespacedPodAsync(nameSpace)).Items.Select(x => x.Metadata.Name).ToArray();
    }

    public async Task<string> GetPodStatus(string podName, string nameSpace, CancellationToken token)
    {
        var pod = await client.ReadNamespacedPodStatusAsync(podName, nameSpace, cancellationToken: token);
        return pod.Status.Phase;
    }

    public async Task<ExecutionInfo> DeleteAsync(string path, CancellationToken _)
    {
        // Ignore the parent token. It should never cancel the delete on shutdown
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await EnsureContextIsAllowed(cts.Token);
        return await this.RunAsync([
            "delete", "--ignore-not-found", "--wait=false", "--now=true", "-f", $"\"{path}\""
        ], wait: true, token: cts.Token);
    }
}
