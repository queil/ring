// ReSharper disable InconsistentNaming

namespace Queil.Ring.DotNet.Cli.Logging;

public enum LogEvent
{
    INIT = 0,
    START = 1,
    HEALTH = 2,
    RECOVERY = 3,
    STOP = 4,
    DESTROY = 5,
    EXCLUDE = 6,
    CONFIG = 7,
    GIT = 8,
    TRACE = 9
}
