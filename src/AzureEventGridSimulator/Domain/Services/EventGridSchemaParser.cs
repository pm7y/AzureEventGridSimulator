using System;
using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Parses events using the Azure Event Grid schema.
/// </summary>
public class EventGridSchemaParser : IEventSchemaParser
{
    /// <inheritdoc />
    public EventSchema Schema => EventSchema.EventGridSchema;

    /// <inheritdoc />
    public SimulatorEvent[] Parse(HttpContext context, string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("Request body is empty.");
        }

        EventGridEvent[] events;

        try
        {
            events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse Event Grid events: {ex.Message}",
                ex
            );
        }

        if (events == null || events.Length == 0)
        {
            throw new InvalidOperationException("No events found in the request body.");
        }

        return events.Select(SimulatorEvent.FromEventGridEvent).ToArray();
    }

    /// <inheritdoc />
    public void Validate(SimulatorEvent[] events)
    {
        foreach (var evt in events)
        {
            evt.Validate();
        }
    }
}
