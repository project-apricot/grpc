using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace ApricotFramework.Grpc.ErrorDefinitions.Serialization;

/// <summary>
/// Moves an error's payload between its .NET form and the protobuf struct that carries it.
/// </summary>
/// <remarks>
/// Both directions go through JSON — the same encoding the HTTP side of the error contract uses — so a
/// payload cannot mean one thing over gRPC and another over HTTP.
/// </remarks>
internal static class PayloadStruct
{
    /// <summary>
    /// Converts a payload to the struct that carries it.
    /// </summary>
    /// <param name="payload">The payload to convert.</param>
    /// <returns>The struct, or null when there is nothing to carry or the payload cannot be encoded.</returns>
    /// <remarks>
    /// Answers null rather than throwing on a payload JSON cannot express: this runs while a failure is
    /// being reported, and losing the payload beats losing the error.
    /// </remarks>
    internal static Struct? ToStruct(IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        try
        {
            return JsonParser.Default.Parse<Struct>(JsonSerializer.Serialize(payload));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            // A value System.Text.Json refuses to write, such as one with a cycle.
            return null;
        }
        catch (InvalidProtocolBufferException)
        {
            // Valid JSON that a struct cannot hold, such as a number outside the double range.
            return null;
        }
    }

    /// <summary>
    /// Converts a carried struct back to a payload.
    /// </summary>
    /// <param name="value">The struct to convert.</param>
    /// <returns>The payload, or null when there is nothing in it or it cannot be read.</returns>
    /// <remarks>
    /// Values arrive as <see cref="JsonElement"/>, matching what the HTTP side hands a reader: a reader
    /// cannot know what a value was meant to be.
    /// </remarks>
    internal static IReadOnlyDictionary<string, object?>? ToPayload(Struct? value)
    {
        if (value is null || value.Fields.Count == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(JsonFormatter.Default.Format(value));

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : property.Value.Clone();
            }

            return payload;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // The formatter refuses a struct the peer built by hand, such as a value with no kind set.
            return null;
        }
    }
}
