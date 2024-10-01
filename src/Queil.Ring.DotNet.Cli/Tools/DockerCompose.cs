namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class DockerCompose(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; } = "docker-compose";
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();
    public ILogger<ITool> Logger { get; } = logger;

    public async Task<ExecutionInfo> RmAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "rm", "-f"], foreground: true, token: token);

    public async Task<ExecutionInfo> PullAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "pull"], foreground: true, token: token);

    public async Task<ExecutionInfo> UpAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "up", "--force-recreate"], foreground: true,
            token: token);

    public async Task<ExecutionInfo> DownAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "down"], foreground: true, token: token);

    public async Task<ExecutionInfo> StopAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "stop"], foreground: true, token: token);
}
