using System;
using Microsoft.Extensions.Logging;
using Queil.Ring.DotNet.Cli.Abstractions.Tools;

namespace Queil.Ring.DotNet.Cli.Tools;

public class ProcessRunner(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; }
    public string[] DefaultArgs { get; set; } = Array.Empty<string>();
    public ILogger<ITool> Logger { get; } = logger;
}
