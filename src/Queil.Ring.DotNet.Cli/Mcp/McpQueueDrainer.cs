namespace Queil.Ring.DotNet.Cli.Mcp;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Logging;

internal sealed class McpQueueDrainer(
    IServer server,
    IReceiver queue,
    IHostApplicationLifetime lifetime,
    ILogger<McpQueueDrainer> logger) : IHostedService
{
    private Task _loop = Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await server.InitializeAsync(cancellationToken);
        _loop = DrainAsync();

        lifetime.ApplicationStopping.Register(async () =>
        {
            using var _ = logger.WithHostScope(LogEvent.DESTROY);
            await server.TerminateAsync(CancellationToken.None);
            logger.LogInformation("Workspace terminated");
            await _loop;
            queue.Complete();
        }, true);
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await _loop;

    private async Task DrainAsync()
    {
        while (await queue.WaitToReadAsync(lifetime.ApplicationStopped))
        {
            if (!await queue.DequeueAsync(_ => Task.CompletedTask)) break;
        }
    }
}
