namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Runnables;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;
using static Dtos.DetailsKeys;

public class AspNetCoreApp(
    AspNetCore config,
    DotnetCliBundle dotnet,
    ILogger<AspNetCoreApp> logger,
    ISender sender,
    GitClone gitClone)
    : DotnetAppBase<AspNetCoreContext, AspNetCore>(config, dotnet, logger, sender, gitClone)
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
