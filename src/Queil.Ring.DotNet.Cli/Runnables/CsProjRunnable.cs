namespace Queil.Ring.DotNet.Cli.Runnables;

using System.Threading;
using System.Threading.Tasks;
using Abstractions.Context;
using Configuration;
using CsProj;
using Infrastructure;
using Microsoft.Extensions.Logging;

public abstract class CsProjRunnable<TContext, TConfig>(
    TConfig config,
    ILogger<CsProjRunnable<TContext, TConfig>> logger,
    ISender sender)
    : ProcessRunnable<TContext, TConfig>(config, logger, sender)
    where TContext : ITrackProcessId, ICsProjContext, ITrackRetries
    where TConfig : IRunnableConfig, IUseCsProjFile
{
    public override string UniqueId => Config.GetProjName();
    protected abstract TContext CreateContext();

    protected override Task<TContext> InitAsync(CancellationToken token) => Task.FromResult(CreateContext());
}
