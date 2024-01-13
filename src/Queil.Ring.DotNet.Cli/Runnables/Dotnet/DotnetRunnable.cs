using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.DotNet.Cli.CsProj;
using Microsoft.Extensions.Logging;
using Queil.Ring.Configuration;
using Queil.Ring.DotNet.Cli.Infrastructure;
using Queil.Ring.DotNet.Cli.Tools;

namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

public abstract class DotnetRunnableBase<TContext, TConfig> : ProcessRunnable<TContext, TConfig>
    where TContext : DotnetContext
    where TConfig : IUseCsProjFile, IRunnableConfig
{
    protected readonly DotnetCliBundle Dotnet;
    private readonly ILogger<DotnetRunnableBase<TContext, TConfig>> _logger;
    private readonly GitClone _gitClone;

    protected DotnetRunnableBase(TConfig config,
        DotnetCliBundle dotnet,
        ILogger<DotnetRunnableBase<TContext, TConfig>> logger,
        ISender sender,
        GitClone gitClone
    ) : base(config, logger, sender)
    {
        Dotnet = dotnet;
        _logger = logger;
        _gitClone = gitClone;
    }

    public override string UniqueId => Config.GetProjName();

    protected override async Task<TContext> InitAsync(CancellationToken token)
    {
        if (Config is IFromGit { SshRepoUrl: not null } gitCfg) await _gitClone.CloneOrPullAsync(gitCfg, token, shallow: true, defaultBranchOnly: true);

        var ctx = DotnetContext.Create<TContext, TConfig>(Config, c => _gitClone.ResolveFullClonePath(c));
        if (File.Exists(ctx.EntryAssemblyPath)) return ctx;

        _logger.LogDebug("Building {Project}", ctx.CsProjPath);
        var result =
            await Dotnet.TryAsync(3, TimeSpan.FromSeconds(10), f => f.BuildAsync(ctx.CsProjPath, token), token);

        if (!result.IsSuccess)
        {
            _logger.LogInformation("Build failed | {output}", result.Output);
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