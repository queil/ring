using System.Collections.Generic;

namespace Queil.Ring.DotNet.Cli.Logging;

public class Scope : Dictionary<string, object>
{
    public const string LogEventKey = "LogEvent";
    public const string UniqueIdKey = "UniqueId";
    public static Scope Event(LogEvent logEvent) => new() { { LogEventKey, logEvent } };
}
