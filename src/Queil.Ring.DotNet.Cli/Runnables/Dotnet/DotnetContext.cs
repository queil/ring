using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Queil.Ring.Configuration.Interfaces;
using Queil.Ring.DotNet.Cli.Abstractions.Context;
using Queil.Ring.DotNet.Cli.CsProj;

namespace Queil.Ring.DotNet.Cli.Runnables.Dotnet;

public class DotnetContext : ICsProjContext, ITrackRetries, ITrackProcessId, ITrackProcessOutput
{
    public int ProcessId { get; set; }
    public string Output { get; set; }
    public string CsProjPath { get; set; }
    public string WorkingDir { get; set; }
    public string TargetFramework { get; set; }
    public string TargetRuntime { get; set; }
    public string EntryAssemblyPath { get; set; }
    public string ExePath => Path.ChangeExtension(EntryAssemblyPath, "exe");
    public Dictionary<string,string> Env { get; set; } = new();
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
    public static T Create<T, C>(C config, Func<IFromGit,string> resolveFullClonePath) where C : IUseCsProjFile where T : DotnetContext
    {
        var originalCsProjPath = config.CsProj;
        try
        {
            var ctx = (T)FormatterServices.GetUninitializedObject(typeof(T));

            if (config is IFromGit { SshRepoUrl: not null } fromGit)
            {
                if (Path.IsPathRooted(config.CsProj))
                {
                    throw new InvalidOperationException($"If sshRepoUrl is used csProj must be a relative path but it is {config.CsProj}");
                }

                config.CsProj = Path.Combine(resolveFullClonePath(fromGit), config.CsProj);
            }

            ctx.CsProjPath = config.FullPath;
            (ctx.TargetFramework, ctx.TargetRuntime) = config.GetTargetFrameworkAndRuntime();
            ctx.WorkingDir = config.GetWorkingDir();
            var runtimePathSegment = ctx.TargetRuntime == null ? "" : $"{Path.DirectorySeparatorChar}{ctx.TargetRuntime}";
            ctx.EntryAssemblyPath = Path.Combine(ctx.WorkingDir, $"bin{Path.DirectorySeparatorChar}{config.Configuration}{Path.DirectorySeparatorChar}{ctx.TargetFramework}{runtimePathSegment}{Path.DirectorySeparatorChar}{config.GetProjName()}.dll");
            ctx.Env = config.Env;
            return ctx;
        }
        finally
        {
            config.CsProj = originalCsProjPath;
        }
    }
}