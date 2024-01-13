using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.DotNet.Cli.CsProj;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Abstractions;
using Queil.Ring.DotNet.Cli.Dtos;
using Queil.Ring.DotNet.Cli.Infrastructure;
using Queil.Ring.DotNet.Cli.Tools.Windows;
using IISExpressConfig = Queil.Ring.Configuration.Runnables.IISExpress;

namespace Queil.Ring.DotNet.Cli.Runnables.Windows.IISExpress;

public class IISExpressRunnable(
    IISExpressConfig config,
    IISExpressExe iisExpress,
    ILogger<IISExpressRunnable> logger,
    ISender sender,
    Func<Uri, HttpClient> clientFactory)
    : CsProjRunnable<IISExpressContext, IISExpressConfig>(config, logger, sender)
{
    private readonly List<string> _wcfServices = [];

    protected override IISExpressContext CreateContext()
    {
        AddDetail(DetailsKeys.CsProjPath, Config.FullPath);
        var uri = Config.GetIISUrl();

        var ctx = new IISExpressContext
        {
            CsProjPath = Config.Csproj,
            WorkingDir = Config.GetWorkingDir(),
            EntryAssemblyPath = $@"{Config.GetWorkingDir()}\bin\{Config.GetProjName()}.dll",
            Uri = uri
        };

        AddDetail(DetailsKeys.WorkDir, ctx.WorkingDir);
        AddDetail(DetailsKeys.ProcessId, ctx.ProcessId);
        AddDetail(DetailsKeys.Uri, ctx.Uri);

        return ctx;
    }

    protected override async Task<IISExpressContext> InitAsync(CancellationToken token)
    {
        var ctx = await base.InitAsync(token);
        _wcfServices.AddRange(new DirectoryInfo(ctx.WorkingDir).EnumerateFiles("*.svc", SearchOption.TopDirectoryOnly).Select(f => f.Name));
        var apphostConfig = new ApphostConfig { VirtualDir = ctx.WorkingDir, Uri = ctx.Uri };
        ctx.TempAppHostConfigPath = apphostConfig.Ensure();
        return ctx;
    }

    protected override async Task StartAsync(IISExpressContext ctx, CancellationToken token)
    {
        var result = await iisExpress.StartWebsite(ctx.TempAppHostConfigPath, token);
        ctx.ProcessId = result.Pid;
        ctx.Output = result.Output;
        logger.LogInformation("{Uri}", ctx.Uri);
    }

    protected override async Task<HealthStatus> CheckHealthAsync(IISExpressContext ctx, CancellationToken token)
    {
        var processCheck = await base.CheckHealthAsync(ctx, token);

        if (processCheck != HealthStatus.Ok) return processCheck;
        try
        {
            foreach (var s in _wcfServices)
            {
                if (token.IsCancellationRequested) continue;
                var client = clientFactory(ctx.Uri);
                using var rq = new HttpRequestMessage(HttpMethod.Get, s);
                var response = await client.SendAsync(rq, HttpCompletionOption.ResponseHeadersRead, token);
                var isHealthy = response is { StatusCode: HttpStatusCode.OK };
                if (isHealthy) continue;

                logger.LogError("Endpoint {ServiceName} failed.", s);
                return HealthStatus.Unhealthy;
            }

            return HealthStatus.Ok;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HealthCheck Failed {UniqueId}", UniqueId);
            return HealthStatus.Unhealthy;
        }
    }
}
