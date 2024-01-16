using static Queil.Ring.DotNet.Cli.Tools.ToolExtensions;
using KustomizeConfig = Queil.Ring.Configuration.Runnables.Kustomize;

namespace Queil.Ring.DotNet.Cli.Runnables.Kustomize;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Dtos;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tools;

public class KustomizeApp(
    KustomizeConfig config,
    IOptions<RingConfiguration> ringCfg,
    ILogger<App<KustomizeContext, KustomizeConfig>> logger,
    ISender sender,
    KubectlBundle bundle)
    : App<KustomizeContext, KustomizeConfig>(config, logger, sender)
{
    private const string NamespacesPath = "{range .items[?(@.kind=='Namespace')]}{.metadata.name}{'\\n'}{end}";

    private readonly string _cacheDir = ringCfg?.Value?.Kustomize.CachePath ??
                                        throw new ArgumentNullException(nameof(RingConfiguration.Kustomize.CachePath));

    private readonly ILogger<App<KustomizeContext, KustomizeConfig>> _logger = logger;

    public override string UniqueId => Config.Path;
    protected override TimeSpan HealthCheckPeriod => TimeSpan.FromSeconds(10);
    protected override int MaxTotalFailuresUntilDead => 10;
    protected override int MaxConsecutiveFailuresUntilDead => 5;

    private string GetCachePath(string inputDir)
    {
        var fileName = Regex.Replace(inputDir, @"[@\.:/\\]", "-");
        return Path.Combine(_cacheDir, $"{fileName}.yaml");
    }

    private async Task<bool> WaitAllPodsAsync(KustomizeContext ctx, CancellationToken token, params string[] statuses)
    {
        return (await Task.WhenAll(
            ctx.Namespaces.Select(async n =>
            {
                async Task<string[]> GetPodsAsync()
                {
                    var pods = await bundle.GetPods(n.Name);
                    _logger.LogDebug("Pods: {pods}", [pods]);
                    return pods;
                }

                var podsNow = await GetPodsAsync();
                if (n.Pods.Any() && !podsNow.Any()) return false;
                n.Pods = podsNow;

                return (await Task.WhenAll(n.Pods.Select(async p =>
                {
                    try
                    {
                        var result = await bundle.GetPodStatus(p, n.Name, token);
                        return statuses.Contains(result);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not get pod status");
                        return false;
                    }
                }))).All(x => x);
            }))).All(x => x);
    }

    protected override async Task<KustomizeContext> InitAsync(CancellationToken token)
    {
        var kustomizationDir = Config.IsRemote() ? Config.Path : Config.FullPath;
        AddDetail(DetailsKeys.KustomizationDir, kustomizationDir);
        var ctx = new KustomizeContext
        {
            KustomizationDir = kustomizationDir,
            CachePath = GetCachePath(kustomizationDir)
        };
        Directory.CreateDirectory(_cacheDir);

        if (!File.Exists(ctx.CachePath) || !await bundle.IsValidManifestAsync(ctx.CachePath, token))
        {
            var kustomizeResult = await bundle.KustomizeBuildAsync(kustomizationDir, ctx.CachePath, token);
            _logger.LogDebug(kustomizeResult.Output);
        }
        else
        {
            _logger.LogInformation("Found cached manifest: {CachedManifestPath}", ctx.CachePath);
        }

        var applyResult = await bundle.TryAsync(10, TimeSpan.FromSeconds(2),
            async t => await bundle.ApplyJsonPathAsync(ctx.CachePath, NamespacesPath, token), token);

        _logger.LogDebug(applyResult.Output);

        if (!applyResult.IsSuccess) throw new InvalidOperationException("Could not apply manifest");
        var namespaces = applyResult.Output.Split(Environment.NewLine);

        ctx.Namespaces = namespaces.Select(n => new Namespace { Name = n }).ToArray();

        return ctx;
    }

    protected override async Task StartAsync(KustomizeContext ctx, CancellationToken token)
    {
        await TryAsync(100, TimeSpan.FromSeconds(6),
            async () => await WaitAllPodsAsync(ctx, token, PodStatus.Running, PodStatus.Error), r => r, token);
        AddDetail(DetailsKeys.Pods,
            string.Join("|", ctx.Namespaces.SelectMany(n => n.Pods.Select(p => n.Name + "/" + p))));
    }

    protected override async Task<HealthStatus> CheckHealthAsync(KustomizeContext ctx, CancellationToken token)
    {
        if (ctx.Namespaces == null) return HealthStatus.Dead;
        var podsHealthy = await WaitAllPodsAsync(ctx, token, PodStatus.Running);
        return podsHealthy ? HealthStatus.Ok : HealthStatus.Unhealthy;
    }

    protected override Task StopAsync(KustomizeContext ctx, CancellationToken token) => Task.CompletedTask;

    protected override async Task DestroyAsync(KustomizeContext ctx, CancellationToken token)
    {
        await bundle.DeleteAsync(ctx.CachePath, token);
    }

    private static class PodStatus
    {
        public const string Running = "Running";
        public const string Error = "Error";
    }
}
