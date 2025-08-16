namespace Queil.Ring.DotNet.Cli.Runnables.Kustomize;

using Abstractions.Context;

public class KustomizeContext : ITrackRetries
{
    public required string KustomizationDir { get; init; }
    public required string CachePath { get; init; }
    public Namespace[] Namespaces { get; set; } = [];
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}

public class Namespace
{
    public required string Name { get; init; }
    public string[] Pods { get; set; } = [];
}
