namespace Queil.Ring.DotNet.Cli.Tools;

using Abstractions.Tools;
using Microsoft.Extensions.Logging;

public class ProcessRunner(ILogger<ITool> logger) : ITool
{
    public string Command { get; set; }
    public string[] DefaultArgs { get; set; } = [];
    public ILogger<ITool> Logger { get; } = logger;
}
