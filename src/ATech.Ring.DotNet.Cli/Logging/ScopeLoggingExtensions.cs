using System;
using ATech.Ring.DotNet.Cli.Abstractions;
using Microsoft.Extensions.Logging;
using Queil.Ring.Protocol;

namespace ATech.Ring.DotNet.Cli.Logging;

public static class ScopeLoggingExtensions
{
    public const string Red = "\u001b[1;31m";
    public const string Gray = "\u001b[38;5;8m";
    internal static Scope ToScope(this IRunnable r) => new() { [Scope.UniqueIdKey] = r.UniqueId };
    public static void LogDebug<T>(this ILogger<T> logger, LogEventStatus s) => logger.LogDebug("{Status}", s);

    public static void LogInformation<T>(this ILogger<T> logger, LogEventStatus s) =>
        logger.LogInformation("{Status}", s);

    public static void LogContextDebug<T>(this ILogger<T> logger, object context) =>
        logger.LogDebug("{@Context}", context);

    public static IDisposable WithClientScope<T>(this ILogger<T> logger) =>
        logger.BeginScope(new Scope { [Scope.UniqueIdKey] = "PROTOCOL", [Scope.LogEventKey] = "CLIENT" });

    public static IDisposable WithReceivedScope<T>(this ILogger<T> logger, M protocolEvent) =>
        logger.BeginScope(new Scope { [Scope.UniqueIdKey] = protocolEvent , [Scope.LogEventKey] =  "<<" });

    public static IDisposable WithSentScope<T>(this ILogger<T> logger, bool isDelivered, M protocolEvent) =>
        logger.BeginScope(new Scope { [Scope.UniqueIdKey] = protocolEvent, [Scope.LogEventKey] = isDelivered ? ">>" : "->" });

    
    public static IDisposable WithHostScope<T>(this ILogger<T> logger, LogEvent logEvent) =>
        logger.WithScope("HOST", logEvent);

    public static IDisposable WithScope<T>(this ILogger<T> logger, string runtimeId, LogEvent logEvent) =>
        logger.BeginScope(new Scope { [Scope.UniqueIdKey] = runtimeId, [Scope.LogEventKey] = logEvent });

    public static IDisposable WithTaskScope<T>(this ILogger<T> logger, string runtimeId, string taskId) =>
        logger.BeginScope(new Scope { [Scope.UniqueIdKey] = $"{runtimeId}:{taskId}", [Scope.LogEventKey] = "TASK" });
    
    public static IDisposable WithLogErrorScope<T>(this ILogger<T> logger) =>
        logger.BeginScope(new Scope
            { [Scope.LogEventKey] = $"{Red}ERROR" });

    public static IDisposable WithLogInfoScope<T>(this ILogger<T> logger) =>
        logger.BeginScope(new Scope
            { [Scope.LogEventKey] = $"{Gray}LOG" });
}