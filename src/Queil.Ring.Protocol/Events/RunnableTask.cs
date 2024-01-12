using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Queil.Ring.Protocol.Events;

public class RunnableTask
{
    public string RunnableId { get; set; } = null!;
    public string TaskId { get; set; } = null!;

    public ReadOnlySpan<byte> Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions.Value);
    public static RunnableTask? Deserialize(ReadOnlySpan<byte> c) => JsonSerializer.Deserialize<RunnableTask>(c, SerializerOptions.Value);

    private static readonly Lazy<JsonSerializerOptions> SerializerOptions = new(() =>
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    });
}
