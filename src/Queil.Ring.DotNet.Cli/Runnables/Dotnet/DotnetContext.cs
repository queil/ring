namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using System;
using System.Collections.Generic;
using System.IO;
using Abstractions.Context;
using Configuration;
using CsProj;
using DotnetConfig = Queil.Ring.Configuration.Runnables.Dotnet;

public class DotnetContext : ICsProjContext, ITrackRetries, ITrackProcessId, ITrackProcessOutput
{
    public string ExePath => Path.ChangeExtension(EntryAssemblyPath, "exe");
    public Dictionary<string, string> Env { get; init; } = [];
    public string[] Urls { get; init; } = [];
    public string[] Args { get; init; } = [];
    public string? CsProjPath { get; init; }
    public required string WorkingDir { get; init; }
    public string? TargetFramework { get; init; }
    public string? TargetRuntime { get; init; }
    public required string EntryAssemblyPath { get; init; }
    public int ProcessId { get; set; }
    public required string Output { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }

    public static DotnetContext Create(DotnetConfig config, Func<IFromGit, string> resolveFullClonePath)
    {
        var originalCsProjPath = config.Csproj;
        try
        {
            if (config.SshRepoUrl is not null)
            {
                if (Path.IsPathRooted(config.Csproj))
                    throw new InvalidOperationException(
                        $"If sshRepoUrl is used csProj must be a relative path but it is {config.Csproj}");

                config.Csproj = Path.Combine(resolveFullClonePath(config), config.Csproj);
            }

            var csProjPath = config.FullPath;
            var (targetFramework, targetRuntime) = config.GetTargetFrameworkAndRuntime();
            var workingDir = config.GetWorkingDir();
            var runtimePathSegment = targetRuntime == null ? "" : $"{Path.DirectorySeparatorChar}{targetRuntime}";

            return new DotnetContext
            {
                CsProjPath = csProjPath,
                TargetFramework = targetFramework,
                TargetRuntime = targetRuntime,
                WorkingDir = workingDir,
                EntryAssemblyPath = Path.Combine(workingDir,
                    $"bin{Path.DirectorySeparatorChar}{config.Configuration}{Path.DirectorySeparatorChar}{targetFramework}{runtimePathSegment}{Path.DirectorySeparatorChar}{config.GetProjName()}.dll"),
                Env = config.Env,
                Urls = [.. config.Urls],
                Args = [.. config.Args],
                Output = string.Empty
            };
        }
        finally
        {
            config.Csproj = originalCsProjPath;
        }
    }
}
