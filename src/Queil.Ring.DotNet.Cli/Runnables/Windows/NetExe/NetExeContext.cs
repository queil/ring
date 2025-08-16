namespace Queil.Ring.DotNet.Cli.Runnables.Windows.NetExe;

using Abstractions.Context;
using CsProj;

public class NetExeContext : ITrackProcessId,
    ITrackProcessOutput,
    ICsProjContext,
    ITrackRetries
{
    public string? CsProjPath { get; set; }
    public required string WorkingDir { get; init; }
    public string? TargetFramework { get; set; }
    public string? TargetRuntime { get; set; }
    public required string EntryAssemblyPath { get; set; }
    public int ProcessId { get; set; }
    public string? Output { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}
