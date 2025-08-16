namespace Queil.Ring.DotNet.Cli.Runnables.Windows.IISExpress;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dtos;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;
using Tools.Windows;
using IISXCoreConfig = Configuration.Runnables.IISXCore;

public class IISXCoreRunnable(
    IISXCoreConfig config,
    IISExpressExe iisExpress,
    ILogger<IISXCoreRunnable> logger,
    ISender sender,
    GitClone gitClone)
    : CsProjRunnable<IISXCoreContext, IISXCoreConfig>(config, logger, sender)
{
    protected override IISXCoreContext CreateContext()
    {
        AddDetail(DetailsKeys.CsProjPath, Config.FullPath);
        var ctx = IISXCoreContext.Create(Config, c => gitClone.ResolveFullClonePath(c));
        AddDetail(DetailsKeys.WorkDir, ctx.WorkingDir);
        AddDetail(DetailsKeys.ProcessId, ctx.ProcessId);
        AddDetail(DetailsKeys.Uri, ctx.Uri);
        return ctx;
    }

    protected override async Task<IISXCoreContext> InitAsync(CancellationToken token)
    {
        var ctx = await base.InitAsync(token);
        var apphostConfig = new ApphostConfig { VirtualDir = ctx.WorkingDir, Uri = ctx.Uri };
        ctx.TempAppHostConfigPath = apphostConfig.Ensure();
        return ctx;
    }

    protected override async Task StartAsync(IISXCoreContext ctx, CancellationToken token)
    {
        var result = await iisExpress.StartWebsite(ctx.TempAppHostConfigPath!, token, new Dictionary<string, string>
        {
            ["LAUNCHER_PATH"] = ctx.ExePath
        });
        ctx.ProcessId = result.Pid;
        ctx.Output = result.Output;
        logger.LogInformation("{Uri}", ctx.Uri);
    }
}
