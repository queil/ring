using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Queil.Ring.Configuration;
using Queil.Ring.DotNet.Cli.Abstractions.Context;
using Queil.Ring.DotNet.Cli.CsProj;
using Queil.Ring.DotNet.Cli.Infrastructure;

namespace Queil.Ring.DotNet.Cli.Runnables;

public abstract class CsProjRunnable<TContext, TConfig>(
    TConfig config,
    ILogger<CsProjRunnable<TContext, TConfig>> logger,
    ISender sender)
    : ProcessRunnable<TContext, TConfig>(config, logger, sender)
    where TContext : ITrackProcessId, ICsProjContext, ITrackRetries
    where TConfig : IRunnableConfig, IUseCsProjFile
{
    protected abstract TContext CreateContext();
    protected override Task<TContext> InitAsync(CancellationToken token) => Task.FromResult(CreateContext());
    public override string UniqueId => Config.GetProjName();
}