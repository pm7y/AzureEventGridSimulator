using System;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Factory for creating event schema formatters based on the desired output schema.
/// </summary>
public class EventSchemaFormatterFactory
{
    private readonly EventGridSchemaFormatter _eventGridFormatter;
    private readonly CloudEventSchemaFormatter _cloudEventFormatter;

    public EventSchemaFormatterFactory(
        EventGridSchemaFormatter eventGridFormatter,
        CloudEventSchemaFormatter cloudEventFormatter)
    {
        _eventGridFormatter = eventGridFormatter;
        _cloudEventFormatter = cloudEventFormatter;
    }

    /// <summary>
    /// Gets the appropriate formatter for the specified schema.
    /// </summary>
    /// <param name="schema">The desired output schema.</param>
    /// <returns>The formatter for the schema.</returns>
    public IEventSchemaFormatter GetFormatter(EventSchema schema)
    {
        return schema switch
        {
            EventSchema.EventGridSchema => _eventGridFormatter,
            EventSchema.CloudEventV1_0 => _cloudEventFormatter,
            _ => throw new ArgumentOutOfRangeException(nameof(schema), schema, "Unknown event schema")
        };
    }
}
