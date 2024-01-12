using System;
using System.Collections.Generic;
using System.IO;
using Queil.Ring.Configuration.Interfaces;

namespace Queil.Ring.Configuration.Runnables;

public enum TaskType
{
    Shell
}

public class TaskDefinition
{
    public TaskType Type { get; set; }
    public bool BringDown { get; set; } = false;
    public string Command { get; set; } = null!;
    public string[] Args { get; set; } = Array.Empty<string>();
}

public abstract class RunnableConfigBase : IRunnableConfig
{
    public abstract string UniqueId { get; }
    public string? FriendlyName { get; set; }

    /// <summary>
    /// If implemented in derived class enables overriding the default <see cref="UniqueId"/>
    /// </summary>
    public string? Id { get; set; }

    public HashSet<string> DeclaredPaths { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();

    public static string GetFullPath(string? workDir, string path)
    {
        path = path.Replace("file://", "");
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workDir ?? string.Empty, path));
    }
}