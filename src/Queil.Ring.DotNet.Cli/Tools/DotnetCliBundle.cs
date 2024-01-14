namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Runnables.Dotnet;

public class DotnetCliBundle(ProcessRunner processRunner, ILogger<DotnetCliBundle> logger)
    : ITool
{
    private const string UrlsEnvVar = "ASPNETCORE_URLS";
    public Dictionary<string, string> DefaultEnvVars = new() { ["ASPNETCORE_ENVIRONMENT"] = "Development" };
    public ILogger<ITool> Logger { get; } = logger;
    public string Command { get; set; } = "dotnet";
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();

    public async Task<ExecutionInfo> RunAsync(DotnetContext ctx, CancellationToken token, string[]? urls = null)
    {
        HandleUrls();
        foreach (var (k, v) in ctx.Env) DefaultEnvVars[k] = v;
        if (File.Exists(ctx.ExePath))
        {
            processRunner.Command = ctx.ExePath;
            return await processRunner.RunProcessAsync(ctx.WorkingDir, DefaultEnvVars, null, token);
        }

        if (File.Exists(ctx.EntryAssemblyPath))
            // Using dotnet exec here because dotnet run spawns subprocesses and killing it doesn't actually kill them
            return await this.RunProcessAsync(ctx.WorkingDir, DefaultEnvVars, ["exec", $"\"{ctx.EntryAssemblyPath}\""],
                token);
        throw new InvalidOperationException($"Neither Exe path nor Dll path specified. {ctx.CsProjPath}");

        void HandleUrls()
        {
            if (urls == null) return;
            if (Environment.GetEnvironmentVariable(UrlsEnvVar) == null)
                DefaultEnvVars.TryAdd(UrlsEnvVar, string.Join(';', urls));
            else
                Environment.SetEnvironmentVariable(UrlsEnvVar, string.Join(';', urls));
        }
    }

    public async Task<ExecutionInfo> BuildAsync(string csProjFile, CancellationToken token) =>
        await this.RunProcessWaitAsync(["build", csProjFile, "-v:q", "/nologo", "/nodereuse:false"], token);
}
