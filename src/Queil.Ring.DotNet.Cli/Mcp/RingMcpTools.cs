namespace Queil.Ring.DotNet.Cli.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using ModelContextProtocol.Server;
using Protocol;
using Protocol.Events;
using Workspace;

[McpServerToolType]
internal sealed class RingMcpTools(IServer server, IWorkspaceLauncher launcher)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    [McpServerTool, Description("Load a workspace from a ring.toml file path")]
    public async Task<string> LoadWorkspace(
        [Description("Absolute or relative path to the ring.toml workspace file")] string workspacePath,
        CancellationToken ct)
    {
        var ack = await server.LoadAsync(workspacePath, ct);
        return ack == Ack.Ok ? $"Workspace loaded: {workspacePath}" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Start all services in the loaded workspace")]
    public async Task<string> StartWorkspace(CancellationToken ct)
    {
        var ack = await server.StartAsync(ct);
        return ack == Ack.Ok ? "Workspace started" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Stop all running services without unloading the workspace configuration")]
    public async Task<string> StopWorkspace(CancellationToken ct)
    {
        var ack = await server.StopAsync(ct);
        return ack == Ack.Ok ? "Workspace stopped" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Unload the current workspace and stop all services")]
    public async Task<string> UnloadWorkspace(CancellationToken ct)
    {
        var ack = await server.UnloadAsync(ct);
        return ack == Ack.Ok ? "Workspace unloaded" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Get the current workspace state including all services and their health statuses")]
    public string GetWorkspaceInfo()
    {
        var info = launcher.GetCurrentInfo();
        return JsonSerializer.Serialize(info, JsonOptions);
    }

    [McpServerTool, Description("Include (start) a specific service by its ID")]
    public async Task<string> IncludeRunnable(
        [Description("The runnable service ID as defined in ring.toml")] string id,
        CancellationToken ct)
    {
        var ack = await server.IncludeAsync(id, ct);
        return ack == Ack.Ok ? $"Runnable '{id}' included" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Exclude (stop) a specific service by its ID without stopping others")]
    public async Task<string> ExcludeRunnable(
        [Description("The runnable service ID as defined in ring.toml")] string id,
        CancellationToken ct)
    {
        var ack = await server.ExcludeAsync(id, ct);
        return ack == Ack.Ok ? $"Runnable '{id}' excluded" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Apply a named workspace flavour to switch which services are active")]
    public async Task<string> ApplyFlavour(
        [Description("The flavour name to apply (see GetWorkspaceInfo for available flavours)")] string flavour,
        CancellationToken ct)
    {
        var ack = await server.ApplyFlavourAsync(flavour, ct);
        return ack == Ack.Ok ? $"Flavour '{flavour}' applied" : $"Failed ({ack})";
    }

    [McpServerTool, Description("Execute a predefined task on a specific service")]
    public async Task<string> ExecuteTask(
        [Description("The runnable service ID")] string runnableId,
        [Description("The task ID to execute (see GetWorkspaceInfo for available tasks per service)")] string taskId,
        CancellationToken ct)
    {
        var task = new RunnableTask { RunnableId = runnableId, TaskId = taskId };
        var ack = await server.ExecuteTaskAsync(task, ct);
        return ack switch
        {
            Ack.TaskOk => $"Task '{taskId}' on '{runnableId}' completed successfully",
            Ack.TaskFailed => $"Task '{taskId}' on '{runnableId}' failed",
            Ack.NotFound => $"Runnable '{runnableId}' or task '{taskId}' not found",
            _ => $"Response: {ack}"
        };
    }
}
