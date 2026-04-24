namespace Queil.Ring.DotNet.Cli.Mcp;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Hosting;

public class McpInitializer(IServer server, IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) =>
        await server.InitializeAsync(lifetime.ApplicationStopping);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
