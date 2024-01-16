namespace Queil.Ring.DotNet.Cli.Logging;

using Infrastructure;
using Protocol;
using Protocol.Events;
using Serilog.Core;
using Serilog.Events;

public class BroadcastSink(ISender sender) : ILogEventSink
{
    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Information || !logEvent.Properties.TryGetValue(Scope.Broadcast, out _)) return;
        var message = logEvent.RenderMessage();
        sender.Enqueue(
            Message.RunnableLogs(new RunnableLogLine
            {
                Line = message,
                RunnableId = logEvent.Properties.TryGetValue(Scope.UniqueIdKey, out var uniqueId)
                    ? uniqueId.ToString()
                    : null
            }));
    }
}
