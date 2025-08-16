namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Abstractions.Context;
using Configuration;
using CsProj;

public class DotnetContext : ICsProjContext, ITrackRetries, ITrackProcessId, ITrackProcessOutput
{
    public string ExePath => Path.ChangeExtension(EntryAssemblyPath, "exe");
    public Dictionary<string, string> Env { get; set; } = [];
    public string? CsProjPath { get; set; }
    public required string WorkingDir { get; set; }
    public string? TargetFramework { get; set; }
    public string? TargetRuntime { get; set; }
    public required string EntryAssemblyPath { get; set; }
    public int ProcessId { get; set; }
    public required string Output { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }

    public static T Create<T, C>(C config, Func<IFromGit, string> resolveFullClonePath)
        where C : IUseCsProjFile where T : DotnetContext
    {
        var originalCsProjPath = config.Csproj;
        try
        {
            var ctx = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

            if (config is IFromGit { SshRepoUrl: not null } fromGit)
            {
                if (Path.IsPathRooted(config.Csproj))
                    throw new InvalidOperationException(
                        $"If sshRepoUrl is used csProj must be a relative path but it is {config.Csproj}");

                config.Csproj = Path.Combine(resolveFullClonePath(fromGit), config.Csproj);
            }

            ctx.CsProjPath = config.FullPath;
            (ctx.TargetFramework, ctx.TargetRuntime) = config.GetTargetFrameworkAndRuntime();
            ctx.WorkingDir = config.GetWorkingDir();
            var runtimePathSegment =
                ctx.TargetRuntime == null ? "" : $"{Path.DirectorySeparatorChar}{ctx.TargetRuntime}";
            ctx.EntryAssemblyPath = Path.Combine(ctx.WorkingDir,
                $"bin{Path.DirectorySeparatorChar}{config.Configuration}{Path.DirectorySeparatorChar}{ctx.TargetFramework}{runtimePathSegment}{Path.DirectorySeparatorChar}{config.GetProjName()}.dll");
            ctx.Env = config.Env;
            return ctx;
        }
        finally
        {
            config.Csproj = originalCsProjPath;
        }
    }
}
