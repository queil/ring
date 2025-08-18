namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal static class Env
{
    internal static string ExpandPath(string path)
    {
        // a naive non-Windows OS hack for https://github.com/dotnet/runtime/issues/25792
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            path = string.Join(Path.DirectorySeparatorChar,
                from c in path.Split(Path.DirectorySeparatorChar)
                select c.Contains('$') ? string.Concat(c.Replace('$', '%'), "%") : c);

        return Environment.ExpandEnvironmentVariables(path);
    }
}

public class RingConfiguration
{
    public required GitSettings Git { get; init; }
    public required KustomizeSettings Kustomize { get; init; }
    public required KubernetesSettings Kubernetes { get; init; }
    public required WorkspaceSettings Workspace { get; init; }
    public required HooksConfiguration? Hooks { get; init; }
}

public class WorkspaceSettings
{
    public int StartupSpreadFactor { get; set; }
}

public class GitSettings
{
    private string? _clonePath;

    [DisallowNull]
    public string? ClonePath
    {
        get => _clonePath;
        set => _clonePath = Env.ExpandPath(value);
    }
}

public class KustomizeSettings
{
    private string? _cachePath;

    [DisallowNull]
    public string? CachePath
    {
        get => _cachePath;
        set => _cachePath = Env.ExpandPath(value);
    }
}

public class KubernetesSettings
{
    private string? _configPath;

    [DisallowNull]
    public string? ConfigPath
    {
        get => _configPath;
        set => _configPath = Env.ExpandPath(value);
    }

    public string[]? AllowedContexts { get; set; }
}

public class InitHookConfig
{
    public string? Command { get; set; }
    public string[] Args { get; set; } = [];
}

public class HooksConfiguration
{
    public InitHookConfig? Init { get; set; }
}
