using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Formats events in the CloudEvents v1.0 schema for delivery.
/// Uses structured content mode (all attributes in JSON body).
/// </summary>
public class CloudEventSchemaFormatter : IEventSchemaFormatter
{
    /// <inheritdoc />
    public EventSchema Schema => EventSchema.CloudEventV1_0;

    /// <inheritdoc />
    /// <remarks>
    /// Azure Event Grid sends events "in an array that has a single event",
    /// so we use the batch content type even for single events.
    /// </remarks>
    public string ContentType => Constants.CloudEventsBatchContentType;

    /// <inheritdoc />
    public string Serialize(SimulatorEvent evt)
    {
        var cloudEvent = ConvertToCloudEvent(evt);
        // Azure Event Grid sends events "in an array that has a single event"
        return JsonConvert.SerializeObject(new[] { cloudEvent }, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <inheritdoc />
    public string SerializeArray(IEnumerable<SimulatorEvent> events)
    {
        var cloudEvents = events.Select(ConvertToCloudEvent).ToArray();
        return JsonConvert.SerializeObject(cloudEvents, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetHeaders(SimulatorEvent evt)
    {
        // Structured mode includes all attributes in the body
        // No additional CloudEvents headers needed
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Converts a SimulatorEvent to a CloudEvent.
    /// If the source is already a CloudEvent, returns it directly.
    /// If the source is an EventGridEvent, converts it to CloudEvents format.
    /// </summary>
    private CloudEvent ConvertToCloudEvent(SimulatorEvent evt)
    {
        if (evt.Schema == EventSchema.CloudEventV1_0 && evt.CloudEvent != null)
        {
            return evt.CloudEvent;
        }

        if (evt.Schema == EventSchema.EventGridSchema && evt.EventGridEvent != null)
        {
            return ConvertEventGridToCloudEvent(evt.EventGridEvent);
        }

        throw new InvalidOperationException($"Cannot convert event with schema {evt.Schema} to CloudEvents format.");
    }

    /// <summary>
    /// Converts an EventGridEvent to a CloudEvent.
    /// </summary>
    private CloudEvent ConvertEventGridToCloudEvent(EventGridEvent eventGridEvent)
    {
        return new CloudEvent
        {
            SpecVersion = "1.0",
            Id = eventGridEvent.Id,
            Source = eventGridEvent.Topic ?? "/",
            Type = eventGridEvent.EventType,
            Time = eventGridEvent.EventTime,
            Subject = eventGridEvent.Subject,
            DataContentType = "application/json",
            DataSchema = ConvertDataVersionToSchema(eventGridEvent.DataVersion),
            Data = eventGridEvent.Data
        };
    }

    /// <summary>
    /// Converts a data version to a CloudEvents dataschema URI.
    /// </summary>
    private string ConvertDataVersionToSchema(string dataVersion)
    {
        if (string.IsNullOrEmpty(dataVersion))
        {
            return null;
        }

        // If it's already a URI, return as-is
        if (Uri.TryCreate(dataVersion, UriKind.Absolute, out _))
        {
            return dataVersion;
        }

        // Otherwise, create a simple schema URI
        return $"#/schema/{dataVersion}";
    }
}
