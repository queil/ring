namespace Queil.Ring.DotNet.Cli.Runnables;

using System.Threading;
using System.Threading.Tasks;
using Abstractions.Context;
using Configuration;
using CsProj;
using Infrastructure;
using Microsoft.Extensions.Logging;

public abstract class CsProjApp<TContext, TConfig>(
    TConfig config,
    ILogger<CsProjApp<TContext, TConfig>> logger,
    ISender sender)
    : ProcessApp<TContext, TConfig>(config, logger, sender)
    where TContext : ITrackProcessId, ICsProjContext, ITrackRetries
    where TConfig : IRunnableConfig, IUseCsProjFile
{
    public override string UniqueId => Config.GetProjName();
    protected abstract TContext CreateContext();

    protected override Task<TContext> InitAsync(CancellationToken token) => Task.FromResult(CreateContext());
}
