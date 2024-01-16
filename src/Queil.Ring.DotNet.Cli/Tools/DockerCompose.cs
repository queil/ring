namespace Queil.Ring.DotNet.Cli.Tools;

using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class DockerCompose(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; } = "docker-compose";
    public string[] DefaultArgs { get; set; } = [];
    public ILogger<ITool> Logger { get; } = logger;

    public async Task<ExecutionInfo> RmAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "rm", "-f"], wait: true, token: token);

    public async Task<ExecutionInfo> PullAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "pull"], wait: true, token: token);

    public async Task<ExecutionInfo> UpAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "up", "--force-recreate"], wait: true,
            token: token);

    public async Task<ExecutionInfo> DownAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "down"], wait: true, token: token);

    public async Task<ExecutionInfo> StopAsync(string composeFilePath, CancellationToken token) =>
        await this.RunAsync(["-f", $"\"{composeFilePath}\"", "stop"], wait: true, token: token);
}
