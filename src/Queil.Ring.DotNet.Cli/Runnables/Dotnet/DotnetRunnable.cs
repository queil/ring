namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsProj;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;
using DotnetConfig = Queil.Ring.Configuration.Runnables.Dotnet;
using static Dtos.DetailsKeys;

public class DotnetRunnable(
    DotnetConfig config,
    DotnetCliBundle dotnet,
    ILogger<DotnetRunnable> logger,
    ISender sender,
    GitClone gitClone)
    : ProcessRunnable<DotnetContext, DotnetConfig>(config, logger, sender)
{
    public override string UniqueId => Config.GetProjName();

    protected override async Task<DotnetContext> InitAsync(CancellationToken token)
    {
        AddDetail(CsProjPath, Config.FullPath);

        if (Config.SshRepoUrl is not null) await gitClone.CloneOrPullAsync(Config, token, true, true);

        var ctx = DotnetContext.Create(Config, c => gitClone.ResolveFullClonePath(c));
        AddDetail(WorkDir, ctx.WorkingDir);
        if (ctx.Urls.Length > 0) AddDetail(Dtos.DetailsKeys.Uri, ctx.Urls);

        if (File.Exists(ctx.EntryAssemblyPath)) return ctx;

        logger.LogDebug("Building {Project}", ctx.CsProjPath);
        var result =
            await dotnet.TryAsync(3, TimeSpan.FromSeconds(10), f => f.BuildAsync(ctx.CsProjPath!, token), token);

        if (!result.IsSuccess) logger.LogInformation("Build failed | {output}", result.Output);
        return ctx;
    }

    protected override async Task StartAsync(DotnetContext ctx, CancellationToken token)
    {
        var info = await dotnet.RunAsync(ctx, token);
        ctx.ProcessId = info.Pid;
        AddDetail(ProcessId, ctx.ProcessId);
        ctx.Output = info.Output;
    }
}
