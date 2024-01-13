namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using CsProj;
using Configuration;
using Infrastructure;
using Tools;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public abstract class DotnetRunnableBase<TContext, TConfig>(
    TConfig config,
    DotnetCliBundle dotnet,
    ILogger<DotnetRunnableBase<TContext, TConfig>> logger,
    ISender sender,
    GitClone gitClone)
    : ProcessRunnable<TContext, TConfig>(config, logger, sender)
    where TContext : DotnetContext
    where TConfig : IUseCsProjFile, IRunnableConfig
{
    protected readonly DotnetCliBundle Dotnet = dotnet;

    public override string UniqueId => Config.GetProjName();

    protected override async Task<TContext> InitAsync(CancellationToken token)
    {
        if (Config is IFromGit { SshRepoUrl: not null } gitCfg) await gitClone.CloneOrPullAsync(gitCfg, token, shallow: true, defaultBranchOnly: true);

        var ctx = DotnetContext.Create<TContext, TConfig>(Config, c => gitClone.ResolveFullClonePath(c));
        if (File.Exists(ctx.EntryAssemblyPath)) return ctx;

        logger.LogDebug("Building {Project}", ctx.CsProjPath);
        var result =
            await Dotnet.TryAsync(3, TimeSpan.FromSeconds(10), f => f.BuildAsync(ctx.CsProjPath, token), token);

        if (!result.IsSuccess)
        {
            logger.LogInformation("Build failed | {output}", result.Output);
        }
        return ctx;
    }

    protected override async Task StartAsync(TContext ctx, CancellationToken token)
    {
        var info = await Dotnet.RunAsync(ctx, token);
        ctx.ProcessId = info.Pid;
        ctx.Output = info.Output;
    }
}