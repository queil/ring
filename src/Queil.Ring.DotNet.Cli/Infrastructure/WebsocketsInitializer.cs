namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class WebsocketsInitializer(WebsocketsHandler handler) : IHostedService
{
    private Task? _messageLoop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageLoop = handler.InitializeAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_messageLoop is { } t) await t;
    }
}
