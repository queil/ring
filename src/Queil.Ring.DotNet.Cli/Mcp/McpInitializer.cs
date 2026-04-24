namespace Queil.Ring.DotNet.Cli.Mcp;

using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Cli;
using Microsoft.Extensions.Hosting;

public class McpInitializer(IServer server, BaseOptions options, IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = lifetime.ApplicationStopping;
        await server.InitializeAsync(token);

        if (options is ConsoleOptions { WorkspacePath: { } path })
        {
            await server.LoadAsync(path, token);
            await server.StartAsync(token);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
