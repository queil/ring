namespace Queil.Ring.DotNet.Cli.Runnables.DockerCompose;

using Abstractions.Context;

public class DockerComposeContext : ITrackProcessId, ITrackRetries
{
    public required string ComposeFilePath { get; init; }
    public int ProcessId { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}
