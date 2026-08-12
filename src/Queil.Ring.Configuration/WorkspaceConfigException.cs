namespace Queil.Ring.Configuration;

using System;
using System.Linq;

public sealed class WorkspaceConfigException(string path, string[] problems)
    : Exception($"Unsupported runnable types in '{path}': {string.Join(' ', problems)}")
{
    public string Path { get; } = path;
    public string[] Problems { get; } = problems;
}
