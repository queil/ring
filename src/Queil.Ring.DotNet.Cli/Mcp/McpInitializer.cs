namespace Queil.Ring.DotNet.Cli.Mcp;

using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Cli;
using Microsoft.Extensions.Hosting;

public class McpInitializer(IServer server, McpOptions options, IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = lifetime.ApplicationStopping;
        await server.InitializeAsync(token);

        if (options.WorkspacePath is { } path)
        {
            await server.LoadAsync(path, token);
            await server.StartAsync(token);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
