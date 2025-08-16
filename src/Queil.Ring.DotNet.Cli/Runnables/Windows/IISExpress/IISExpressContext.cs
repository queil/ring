namespace Queil.Ring.DotNet.Cli.Runnables.Windows.IISExpress;

using System;
using Abstractions.Context;
using CsProj;

public class IISExpressContext : ITrackProcessId, ITrackProcessOutput, ICsProjContext, ITrackRetries, ITrackUri
{
    public string? TempAppHostConfigPath { get; set; }
    public required string CsProjPath { get; init; }
    public required string WorkingDir { get; init; }
    public string? TargetFramework { get; set; }
    public string? TargetRuntime { get; set; }
    public required string EntryAssemblyPath { get; set; }
    public int ProcessId { get; set; }
    public string? Output { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
    public required Uri Uri { get; init; }
}
