namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class ProcessRunner(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; }
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();
    public ILogger<ITool> Logger { get; } = logger;
}
