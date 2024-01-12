using System;
using Queil.Ring.DotNet.Cli.Abstractions.Context;

namespace Queil.Ring.DotNet.Cli.Runnables.Kustomize;

public class KustomizeContext : ITrackRetries
{
    public string KustomizationDir { get; set; }
    public string CachePath { get; set; }
    public Namespace[] Namespaces { get; set; } = Array.Empty<Namespace>();
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
}

public class Namespace
{
    public string Name { get; set; }
    public string[] Pods { get; set; } = Array.Empty<string>();
}