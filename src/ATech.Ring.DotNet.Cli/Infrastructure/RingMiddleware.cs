namespace ATech.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class RingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebsocketsHandler _socketManager;

    public RingMiddleware(RequestDelegate next, WebsocketsHandler broadcast)
    {
        _next = next;
        _socketManager = broadcast;
    }

    public async Task Invoke(HttpContext context)
    {
        var clientId = Guid.Empty;
        try
        {
            if (!await context.ShouldHandle(_next)) return;
            var log = context.Logger();

            const string clientIdKey = "clientId";
            if (!context.Request.Query.ContainsKey(clientIdKey))
            {
                var errorSocket = await context.WebSockets.AcceptWebSocketAsync();
                await errorSocket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError,
                    "clientId (uuid) expected in query string.", context.RequestAborted);
                return;
            }

            if (!Guid.TryParse(context.Request.Query[clientIdKey], out clientId))
            {
                var errorSocket = await context.WebSockets.AcceptWebSocketAsync();
                await errorSocket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError,
                    "clientId is not a valid Uuid / Guid.", context.RequestAborted);
                return;
            }

            await _socketManager.ListenAsync(clientId, () =>
            {
                var s = context.WebSockets.AcceptWebSocketAsync();
                using (log.WithClientScope()) log.LogInformation("Client {clientId} connected", clientId);
                return s;
            }, context.Get<IHostApplicationLifetime>().ApplicationStopped);
        }
        catch (OperationCanceledException)
        {
            using (context.Logger().WithClientScope())
                context.Logger().LogInformation("Client {clientId} disconnected", clientId);
        }
        catch (Exception ex)
        {
            using (context.Logger().WithClientScope()) context.Logger().LogError("Unhandled: {ex}", ex);
        }
    }
}