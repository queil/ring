namespace Queil.Ring.DotNet.Cli.Runnables.DockerCompose;

using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Dtos;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;
using DockerComposeConfig = Configuration.Runnables.DockerCompose;

public class DockerComposeRunnable(
    DockerComposeConfig config,
    ILogger<Runnable<DockerComposeContext, DockerComposeConfig>> logger,
    ISender sender,
    DockerCompose dockerCompose)
    : Runnable<DockerComposeContext, DockerComposeConfig>(config, logger, sender)
{
    public override string UniqueId => Config.Path;

    protected override async Task<DockerComposeContext> InitAsync(CancellationToken token)
    {
        AddDetail(DetailsKeys.DockerComposePath, Config.FullPath);
        var ctx = new DockerComposeContext { ComposeFilePath = Config.FullPath };
        await dockerCompose.RmAsync(ctx.ComposeFilePath, token);
        await dockerCompose.PullAsync(ctx.ComposeFilePath, token);
        return ctx;
    }

    protected override async Task StartAsync(DockerComposeContext ctx, CancellationToken token)
    {
        var result = await dockerCompose.UpAsync(ctx.ComposeFilePath, token);
        ctx.ProcessId = result.Pid;
    }

    protected override Task<HealthStatus> CheckHealthAsync(DockerComposeContext ctx, CancellationToken token) =>
        Task.FromResult(ProcessExtensions.IsProcessRunning(ctx.ProcessId) ? HealthStatus.Ok : HealthStatus.Dead);

    protected override async Task StopAsync(DockerComposeContext ctx, CancellationToken token)
    {
        await dockerCompose.StopAsync(ctx.ComposeFilePath, token);
    }

    protected override async Task DestroyAsync(DockerComposeContext ctx, CancellationToken token)
    {
        await dockerCompose.DownAsync(ctx.ComposeFilePath, token);
    }
}
