// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global

namespace Queil.Ring.Configuration.Runnables;

public enum TaskType
{
    Shell
}

public class TaskDefinition
{
    public TaskType Type { get; init; }
    public bool BringDown { get; init; }
    public required string Command { get; init; }
    public List<string> Args { get; } = [];
}

public abstract class RunnableConfigBase : IRunnableConfig
{
    public string? Id { get; set; }
    public abstract string UniqueId { get; }
    public abstract string TypeId { get; }
    public string? FriendlyName { get; init; }

    public HashSet<string> DeclaredPaths { get; } = [];

    public List<string> Tags { get; } = [];

    public Dictionary<string, TaskDefinition> Tasks { get; } = [];

    public Dictionary<string, string> Env { get; } = [];

    protected static string GetFullPath(string? workDir, string path)
    {
        path = path.Replace("file://", "");
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workDir ?? string.Empty, path));
    }
}
