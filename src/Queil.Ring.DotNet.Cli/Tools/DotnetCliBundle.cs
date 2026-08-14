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
    public ILogger<ITool> Logger { get; } = logger;
    public string Command { get; set; } = "dotnet";
    public string[] DefaultArgs { get; set; } = [];

    public async Task<ExecutionInfo> RunAsync(DotnetContext ctx, CancellationToken token)
    {
        var envVars = new Dictionary<string, string>();
        if (ctx.Urls.Length > 0) envVars[UrlsEnvVar] = string.Join(';', ctx.Urls);
        foreach (var (k, v) in ctx.Env) envVars[k] = v;

        if (File.Exists(ctx.ExePath))
        {
            processRunner.Command = ctx.ExePath;
            return await processRunner.RunAsync(ctx.Args, ctx.WorkingDir, envVars, token: token);
        }

        if (File.Exists(ctx.EntryAssemblyPath))
            // Using dotnet exec here because dotnet run spawns subprocesses and killing it doesn't actually kill them
            return await this.RunAsync(["exec", $"\"{ctx.EntryAssemblyPath}\"", .. ctx.Args],
                workingDirectory: ctx.WorkingDir, envVars: envVars,
                token: token);
        throw new InvalidOperationException($"Neither Exe path nor Dll path specified. {ctx.CsProjPath}");
    }

    public async Task<ExecutionInfo> BuildAsync(string csProjFile, CancellationToken token) =>
        await this.RunAsync(["build", csProjFile, "-v:q", "/nologo", "/nodereuse:false"], foreground: true,
            token: token);
}
