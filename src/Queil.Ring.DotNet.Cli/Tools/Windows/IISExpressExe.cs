namespace Queil.Ring.DotNet.Cli.Tools.Windows;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class IISExpressExe(ILogger<IISExpressExe> logger) : ITool
{
    public string Command { get; set; } = @"C:\Program Files\IIS Express\iisexpress.exe";
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();
    public ILogger<ITool> Logger { get; } = logger;

    private void OnError(string error)
    {
        var message = error.StartsWith("Failed to register URL")
            ? $"The port might be used another process. Try 'netstat -na -o' or if ports are reserved 'netsh interface ipv4 show excludedportrange protocol=tcp'). Original error: {error}"
            : $"IISExpress failed. Original error: {error}";

        Logger.LogError("{Message}", message);
    }

    public async Task<ExecutionInfo> StartWebsite(string configPath, CancellationToken token,
        IDictionary<string, string>? envVars = null) =>
        await this.RunAsync([$"/config:\"{configPath}\"", "/siteid:1"], envVars: envVars,
            onErrorData: OnError, token: token);
}
