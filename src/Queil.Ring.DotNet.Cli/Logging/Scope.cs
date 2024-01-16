namespace Queil.Ring.DotNet.Cli.Logging;

using System.Collections.Generic;

public class Scope : Dictionary<string, object>
{
    public const string LogEventKey = "LogEvent";
    public const string UniqueIdKey = "UniqueId";
    public const string Broadcast = "Broadcast";
    public static Scope Event(LogEvent logEvent) => new() { { LogEventKey, logEvent } };
}
