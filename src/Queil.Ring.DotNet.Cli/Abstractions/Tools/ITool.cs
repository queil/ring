namespace Queil.Ring.DotNet.Cli.Abstractions.Tools;

using Microsoft.Extensions.Logging;

public interface ITool
{
    string Command { get; set; }
    string[] DefaultArgs { get; set; }
    ILogger<ITool> Logger { get; }
}
