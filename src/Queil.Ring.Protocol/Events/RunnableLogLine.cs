namespace Queil.Ring.Protocol.Events;

using System;
using MessagePack;

[MessagePackObject]
public class RunnableLogLine
{
    [property: Key(0)]public string RunnableId { get; set; } = null!;
    [property: Key(1)]public string Line { get; set; } = null!;

    public ReadOnlySpan<byte> Serialize() => MessagePackSerializer.Serialize(this);
    public static RunnableLogLine Deserialize(ReadOnlySpan<byte> bytes) => 
        MessagePackSerializer.Deserialize<RunnableLogLine>(bytes.ToArray().AsMemory());
}
