namespace Queil.Ring.DotNet.Cli.Mcp;

using System;
using System.ComponentModel;
using System.IO;
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
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(15);

    [McpServerTool(Name = "get_workspace_info", Title = "Get workspace info")]
    [Description(
        "Returns the current workspace state as JSON: server state (IDLE/LOADED/RUNNING), runnables with their states (ZERO/INITIATED/STARTED/HEALTH_CHECK/HEALTHY/DEAD/RECOVERING), flavours, and tasks. Poll this to observe progress - there are no notifications.")]
    public string GetWorkspaceInfo()
    {
        server.RequestWorkspaceInfo();
        return Encoding.UTF8.GetString(launcher.GetCurrentInfo().Serialize());
    }

    [McpServerTool(Name = "load_workspace", Title = "Load workspace")]
    [Description("Loads a workspace TOML file. Does not start it - call start_workspace next.")]
    public async Task<string> LoadWorkspace(
        [Description("Path to the workspace TOML file")]
        string workspacePath, CancellationToken ct)
    {
        if (!File.Exists(workspacePath)) return $"not found: {workspacePath}";
        var load = server.LoadAsync(workspacePath, ct);
        if (await Task.WhenAny(load, Task.Delay(LoadTimeout, ct)) != load)
            return
                $"load did not complete within {LoadTimeout.TotalSeconds}s - the workspace file is likely invalid. Ring keeps retrying in the background until the file is fixed";
        await load;
        return "loaded";
    }

    [McpServerTool(Name = "start_workspace", Title = "Start workspace")]
    [Description("Starts all runnables of the loaded workspace. Poll get_workspace_info until they are HEALTHY.")]
    public async Task<string> StartWorkspace(CancellationToken ct)
    {
        await server.StartAsync(ct);
        return "started";
    }

    [McpServerTool(Name = "stop_workspace", Title = "Stop workspace")]
    [Description("Stops all runnables of the loaded workspace.")]
    public async Task<string> StopWorkspace(CancellationToken ct)
    {
        await server.StopAsync(ct);
        return "stopped";
    }

    [McpServerTool(Name = "unload_workspace", Title = "Unload workspace")]
    [Description("Stops the workspace (if running) and unloads it.")]
    public async Task<string> UnloadWorkspace(CancellationToken ct)
    {
        await server.UnloadAsync(ct);
        return "unloaded";
    }

    [McpServerTool(Name = "include_runnable", Title = "Include runnable")]
    [Description("Starts a single runnable by its id (as reported by get_workspace_info).")]
    public async Task<string> IncludeRunnable(
        [Description("The runnable id")] string id, CancellationToken ct)
    {
        var ack = await server.IncludeAsync(id, ct);
        return ack == Ack.NotFound ? $"not found: {id}" : "included";
    }

    [McpServerTool(Name = "exclude_runnable", Title = "Exclude runnable")]
    [Description("Stops a single runnable by its id. It goes to the ZERO state.")]
    public async Task<string> ExcludeRunnable(
        [Description("The runnable id")] string id, CancellationToken ct)
    {
        var ack = await server.ExcludeAsync(id, ct);
        return ack == Ack.NotFound ? $"not found: {id}" : "excluded";
    }

    [McpServerTool(Name = "apply_flavour", Title = "Apply flavour")]
    [Description(
        "Applies a workspace flavour (a tag): runnables tagged with it get started, the rest get stopped. Available flavours are listed by get_workspace_info.")]
    public async Task<string> ApplyFlavour(
        [Description("The flavour name")] string flavour, CancellationToken ct)
    {
        var ack = await server.ApplyFlavourAsync(flavour, ct);
        return ack == Ack.NotFound ? $"not found: {flavour}" : "applied";
    }

    [McpServerTool(Name = "list_tasks", Title = "List available tasks")]
    [Description("Lists tasks defined in the loaded workspace, one '<runnableId>/<taskId>' per line.")]
    public string ListTasks()
    {
        var sb = new StringBuilder();
        foreach (var r in launcher.GetCurrentInfo().Runnables)
            foreach (var task in r.Tasks)
                sb.AppendLine($"{r.Id}/{task}");
        return sb.Length > 0 ? sb.ToString() : "no tasks available";
    }

    [McpServerTool(Name = "execute_task", Title = "Execute task")]
    [Description(
        "Runs a task of a runnable (see list_tasks). A task with bringDown = true stops the runnable first and starts it back up if the task succeeds.")]
    public async Task<string> ExecuteTask(
        [Description("The runnable id")] string runnableId,
        [Description("The task id")] string taskId, CancellationToken ct)
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
