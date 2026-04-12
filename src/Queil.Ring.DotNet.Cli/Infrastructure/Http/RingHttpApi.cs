namespace Queil.Ring.DotNet.Cli.Infrastructure.Http;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Protocol;
using Protocol.Events;
using Workspace;

public static class RingHttpApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IEndpointRouteBuilder MapRingApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/ring");

        api.MapGet("/status", ([FromServices] IWorkspaceLauncher launcher) =>
            Results.Json(launcher.CurrentInfo, JsonOptions));

        api.MapPost("/workspace/load", async (LoadRequest req, [FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.LoadAsync(req.Path, ct)));

        api.MapPost("/workspace/start", async ([FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.StartAsync(ct)));

        api.MapPost("/workspace/stop", async ([FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.StopAsync(ct)));

        api.MapPost("/workspace/unload", async ([FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.UnloadAsync(ct)));

        api.MapPost("/workspace/flavour/{flavour}", async (string flavour, [FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.ApplyFlavourAsync(flavour, ct)));

        api.MapPost("/runnable/{id}/start", async (string id, [FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.IncludeAsync(id, ct)));

        api.MapPost("/runnable/{id}/stop", async (string id, [FromServices] IServer server, CancellationToken ct) =>
            AckResult(await server.ExcludeAsync(id, ct)));

        api.MapPost("/runnable/{id}/restart", async (string id, [FromServices] IServer server, CancellationToken ct) =>
        {
            var stopAck = await server.ExcludeAsync(id, ct);
            if (stopAck == Ack.NotFound) return AckResult(stopAck);
            await Task.Delay(1000, ct);
            return AckResult(await server.IncludeAsync(id, ct));
        });

        api.MapPost("/runnable/{id}/task/{taskId}",
            async (string id, string taskId, [FromServices] IServer server, CancellationToken ct) =>
                AckResult(await server.ExecuteTaskAsync(
                    new RunnableTask { RunnableId = id, TaskId = taskId }, ct)));

        return app;
    }

    private static IResult AckResult(Ack ack) => ack switch
    {
        Ack.Ok or Ack.Alive or Ack.TaskOk => Results.Ok(new AckDto("ok")),
        Ack.NotFound                       => Results.NotFound(new AckDto("notFound")),
        Ack.TaskFailed                     => Results.Json(new AckDto("taskFailed"), statusCode: 422),
        _                                  => Results.Problem(title: ack.ToString())
    };

    private record LoadRequest(string Path);
    private record AckDto(string Ack);
}
