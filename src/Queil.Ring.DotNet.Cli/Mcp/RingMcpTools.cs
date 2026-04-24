namespace Queil.Ring.DotNet.Cli.Mcp;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using ModelContextProtocol.Server;
using Protocol;
using Protocol.Events;
using Workspace;

[McpServerToolType]
public class RingMcpTools(IServer server, IWorkspaceLauncher launcher)
{
    [McpServerTool(Name = "get_workspace_info", Title = "Get workspace info")]
    public string GetWorkspaceInfo()
    {
        server.RequestWorkspaceInfo();
        return Encoding.UTF8.GetString(launcher.GetCurrentInfo().Serialize());
    }

    [McpServerTool(Name = "load_workspace", Title = "Load workspace")]
    public async Task<string> LoadWorkspace(string workspacePath, CancellationToken ct)
    {
        await server.LoadAsync(workspacePath, ct);
        return "loaded";
    }

    [McpServerTool(Name = "start_workspace", Title = "Start workspace")]
    public async Task<string> StartWorkspace(CancellationToken ct)
    {
        await server.StartAsync(ct);
        return "started";
    }

    [McpServerTool(Name = "stop_workspace", Title = "Stop workspace")]
    public async Task<string> StopWorkspace(CancellationToken ct)
    {
        await server.StopAsync(ct);
        return "stopped";
    }

    [McpServerTool(Name = "unload_workspace", Title = "Unload workspace")]
    public async Task<string> UnloadWorkspace(CancellationToken ct)
    {
        await server.UnloadAsync(ct);
        return "unloaded";
    }

    [McpServerTool(Name = "include_runnable", Title = "Include runnable")]
    public async Task<string> IncludeRunnable(string id, CancellationToken ct)
    {
        var ack = await server.IncludeAsync(id, ct);
        return ack == Ack.NotFound ? $"not found: {id}" : "included";
    }

    [McpServerTool(Name = "exclude_runnable", Title = "Exclude runnable")]
    public async Task<string> ExcludeRunnable(string id, CancellationToken ct)
    {
        var ack = await server.ExcludeAsync(id, ct);
        return ack == Ack.NotFound ? $"not found: {id}" : "excluded";
    }

    [McpServerTool(Name = "apply_flavour", Title = "Apply flavour")]
    public async Task<string> ApplyFlavour(string flavour, CancellationToken ct)
    {
        var ack = await server.ApplyFlavourAsync(flavour, ct);
        return ack == Ack.NotFound ? $"not found: {flavour}" : "applied";
    }

    [McpServerTool(Name = "execute_task", Title = "Execute task")]
    public async Task<string> ExecuteTask(string runnableId, string taskId, CancellationToken ct)
    {
        var ack = await server.ExecuteTaskAsync(new RunnableTask { RunnableId = runnableId, TaskId = taskId }, ct);
        return ack switch
        {
            Ack.NotFound => $"not found: {runnableId}/{taskId}",
            Ack.TaskFailed => "task failed",
            _ => "ok"
        };
    }
}
