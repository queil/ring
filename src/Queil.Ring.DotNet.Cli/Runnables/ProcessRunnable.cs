namespace Queil.Ring.DotNet.Cli.Runnables;

using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Abstractions.Context;
using Configuration;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Tools;

public abstract class ProcessRunnable<TContext, TConfig>(
    TConfig config,
    ILogger<ProcessRunnable<TContext, TConfig>> logger,
    ISender sender)
    : Runnable<TContext, TConfig>(config, logger, sender)
    where TContext : ITrackProcessId
    where TConfig : IRunnableConfig
{
    protected override Task DestroyAsync(TContext ctx, CancellationToken token) => Task.CompletedTask;

    /// <summary>
    ///     The default implementation checks whether the process exists
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override Task<HealthStatus> CheckHealthAsync(TContext ctx, CancellationToken token) =>
        ctx.ProcessId == 0
            ? Task.FromResult(HealthStatus.Unhealthy)
            : Task.FromResult(ProcessExtensions.IsProcessRunning(ctx.ProcessId)
                ? HealthStatus.Ok
                : HealthStatus.Unhealthy);

    /// <summary>
    ///     The default implementation kills the process
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override Task StopAsync(TContext ctx, CancellationToken token)
    {
        if (ctx == null || ctx.ProcessId == 0) return Task.CompletedTask;
        ProcessExtensions.KillProcess(ctx.ProcessId);
        return Task.CompletedTask;
    }
}
