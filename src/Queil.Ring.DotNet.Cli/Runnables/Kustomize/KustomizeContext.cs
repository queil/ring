namespace Queil.Ring.DotNet.Cli.Runnables.Kustomize;

using Abstractions.Context;

public class KustomizeContext : ITrackRetries
{
    public string KustomizationDir { get; set; }
    public string CachePath { get; set; }
    public Namespace[] Namespaces { get; set; } = [];
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}

public class Namespace
{
    public string Name { get; set; }
    public string[] Pods { get; set; } = [];
}
