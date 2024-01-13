namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using Queil.Ring.Configuration.Runnables;
using Infrastructure;
using Tools;
using static Dtos.DetailsKeys;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class AspNetCoreRunnable(
    AspNetCore config,
    DotnetCliBundle dotnet,
    ILogger<AspNetCoreRunnable> logger,
    ISender sender,
    GitClone gitClone)
    : DotnetRunnableBase<AspNetCoreContext, AspNetCore>(config, dotnet, logger, sender, gitClone)
{
    protected override async Task<AspNetCoreContext> InitAsync(CancellationToken token)
    {
        AddDetail(CsProjPath, Config.FullPath);
        var ctx = await base.InitAsync(token);
        ctx.Urls = Config.Urls.ToArray();
        AddDetail(WorkDir, ctx.WorkingDir);
        AddDetail(ProcessId, ctx.ProcessId);
        if (ctx.Urls.Any()) AddDetail(Uri, ctx.Urls);
        return ctx;
    }

    protected override async Task StartAsync(AspNetCoreContext ctx, CancellationToken token)
    {
        var info = await Dotnet.RunAsync(ctx, token, ctx.Urls);
        ctx.ProcessId = info.Pid;
        ctx.Output = info.Output;
    }
}