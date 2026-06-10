using System.Text.Json;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Formats events in the Azure Event Grid schema for delivery.
/// </summary>
public class EventGridSchemaFormatter(TimeProvider timeProvider) : IEventSchemaFormatter
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        // Azure omits absent fields (e.g. null data) rather than emitting them as null
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public EventSchema Schema => EventSchema.EventGridSchema;

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public string Serialize(SimulatorEvent evt)
    {
        var eventGridEvent = ConvertToEventGridEvent(evt);
        return JsonSerializer.Serialize(new[] { eventGridEvent }, _serializerOptions);
    }

    /// <inheritdoc />
    public string SerializeSingle(SimulatorEvent evt)
    {
        var eventGridEvent = ConvertToEventGridEvent(evt);
        return JsonSerializer.Serialize(eventGridEvent, _serializerOptions);
    }

    /// <inheritdoc />
    public string SerializeArray(IEnumerable<SimulatorEvent> events)
    {
        var eventGridEvents = events.Select(ConvertToEventGridEvent).ToArray();
        return JsonSerializer.Serialize(eventGridEvents, _serializerOptions);
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetHeaders(SimulatorEvent evt)
    {
        // Event Grid schema doesn't require special headers for delivery
        return new Dictionary<string, string>();
    }

    /// <summary>
    ///     Converts a SimulatorEvent to an EventGridEvent.
    ///     If the source is already an EventGridEvent, returns it directly.
    ///     If the source is a CloudEvent, converts it to EventGrid format.
    /// </summary>
    private EventGridEvent ConvertToEventGridEvent(SimulatorEvent evt)
    {
        if (evt.Schema == EventSchema.EventGridSchema && evt.EventGridEvent != null)
        {
            return evt.EventGridEvent;
        }

        if (evt.Schema == EventSchema.CloudEventV1_0 && evt.CloudEvent != null)
        {
            return ConvertCloudEventToEventGrid(evt.CloudEvent);
        }

        throw new InvalidOperationException(
            $"Cannot convert event with schema {evt.Schema} to Event Grid format."
        );
    }

    /// <summary>
    ///     Converts a CloudEvent to an EventGridEvent.
    /// </summary>
    private EventGridEvent ConvertCloudEventToEventGrid(CloudEvent cloudEvent)
    {
        // Azure is lenient - source can be null, use empty string as fallback
        var source = cloudEvent.Source ?? "";
        var eventGridEvent = new EventGridEvent
        {
            Id = cloudEvent.Id,
            Subject = cloudEvent.Subject ?? source,
            EventType = cloudEvent.Type,
            EventTime = cloudEvent.Time ?? timeProvider.GetUtcNow().ToString("o"),
            // CloudEvents binary payloads arrive in data_base64; pass the base64 string
            // through so the payload isn't silently dropped on conversion.
            Data = cloudEvent.Data ?? cloudEvent.DataBase64,
            DataVersion = ExtractDataVersion(cloudEvent.DataSchema),
            MetadataVersion = "1",
        };
        eventGridEvent.SetTopic(source);
        return eventGridEvent;
    }

    /// <summary>
    ///     Extracts a data version from a CloudEvents dataschema URI.
    /// </summary>
    private string ExtractDataVersion(string? dataSchema)
    {
        if (string.IsNullOrEmpty(dataSchema))
        {
            return "";
        }

        // Try to extract version from URI (e.g., "https://example.com/schema/v1" -> "v1").
        // Uri.Segments is only valid on absolute URIs; relative ones (e.g. "#/schema/v1")
        // throw InvalidOperationException, so fall back to a manual split for those.
        if (Uri.TryCreate(dataSchema, UriKind.Absolute, out var uri))
        {
            var segments = uri.Segments;
            if (segments.Length > 0)
            {
                var lastSegment = segments.Last().TrimEnd('/');
                if (lastSegment.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    return lastSegment;
                }
            }

            return dataSchema;
        }

        var lastPart = dataSchema.TrimEnd('/').Split('/').Last();
        if (lastPart.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            return lastPart;
        }

        return dataSchema;
    }
}
