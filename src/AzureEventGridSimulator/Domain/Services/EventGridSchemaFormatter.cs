using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Formats events in the Azure Event Grid schema for delivery.
/// </summary>
public class EventGridSchemaFormatter : IEventSchemaFormatter
{
    /// <inheritdoc />
    public EventSchema Schema => EventSchema.EventGridSchema;

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public string Serialize(SimulatorEvent evt)
    {
        var eventGridEvent = ConvertToEventGridEvent(evt);
        return JsonSerializer.Serialize(new[] { eventGridEvent });
    }

    /// <inheritdoc />
    public string SerializeArray(IEnumerable<SimulatorEvent> events)
    {
        var eventGridEvents = events.Select(ConvertToEventGridEvent).ToArray();
        return JsonSerializer.Serialize(eventGridEvents);
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetHeaders(SimulatorEvent evt)
    {
        // Event Grid schema doesn't require special headers for delivery
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Converts a SimulatorEvent to an EventGridEvent.
    /// If the source is already an EventGridEvent, returns it directly.
    /// If the source is a CloudEvent, converts it to EventGrid format.
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
    /// Converts a CloudEvent to an EventGridEvent.
    /// </summary>
    private EventGridEvent ConvertCloudEventToEventGrid(CloudEvent cloudEvent)
    {
        return new EventGridEvent
        {
            Id = cloudEvent.Id,
            Subject = cloudEvent.Subject ?? cloudEvent.Source,
            EventType = cloudEvent.Type,
            EventTime = cloudEvent.Time ?? DateTime.UtcNow.ToString("o"),
            Data = cloudEvent.Data,
            DataVersion = ExtractDataVersion(cloudEvent.DataSchema),
            Topic = cloudEvent.Source,
            MetadataVersion = "1",
        };
    }

    /// <summary>
    /// Extracts a data version from a CloudEvents dataschema URI.
    /// </summary>
    private string ExtractDataVersion(string dataSchema)
    {
        if (string.IsNullOrEmpty(dataSchema))
        {
            return "";
        }

        // Try to extract version from URI (e.g., "/schema/v1" -> "v1")
        if (Uri.TryCreate(dataSchema, UriKind.RelativeOrAbsolute, out var uri))
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
        }

        return dataSchema;
    }
}
