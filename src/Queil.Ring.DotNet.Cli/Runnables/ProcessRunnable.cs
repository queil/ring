﻿using System.Threading;
using System.Threading.Tasks;
using Queil.Ring.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Abstractions;
using Queil.Ring.DotNet.Cli.Abstractions.Context;
using Queil.Ring.DotNet.Cli.Infrastructure;
using Queil.Ring.DotNet.Cli.Tools;

namespace Queil.Ring.DotNet.Cli.Runnables;

public abstract class ProcessRunnable<TContext, TConfig> : Runnable<TContext, TConfig>
    where TContext : ITrackProcessId
    where TConfig : IRunnableConfig
{
    protected ProcessRunnable(TConfig config, ILogger<ProcessRunnable<TContext, TConfig>> logger, ISender sender) : base(config, logger, sender)
    {
    }

    protected override Task DestroyAsync(TContext ctx, CancellationToken token) => Task.CompletedTask;

    /// <summary>
    /// The default implementation checks whether the process exists
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override Task<HealthStatus> CheckHealthAsync(TContext ctx, CancellationToken token)
    {
        return ctx.ProcessId == 0 ? 
            Task.FromResult(HealthStatus.Unhealthy) 
            : Task.FromResult(ProcessExtensions.IsProcessRunning(ctx.ProcessId) ? HealthStatus.Ok : HealthStatus.Unhealthy);
    }

    /// <summary>
    /// The default implementation kills the process
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