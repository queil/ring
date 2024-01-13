namespace Queil.Ring.DotNet.Cli.Runnables.DockerCompose;

using Abstractions.Context;

public class DockerComposeContext : ITrackProcessId, ITrackRetries
{
    public string ComposeFilePath { get; set; }
    public int ProcessId { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}
