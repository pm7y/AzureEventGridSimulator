using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Factory for creating event schema formatters based on the desired output schema.
/// </summary>
public class EventSchemaFormatterFactory(
    EventGridSchemaFormatter eventGridFormatter,
    CloudEventSchemaFormatter cloudEventFormatter
)
{
    /// <summary>
    /// Gets the appropriate formatter for the specified schema.
    /// </summary>
    /// <param name="schema" >
    /// The desired output schema.
    /// </param>
    /// <returns>
    /// The formatter for the schema.
    /// </returns>
    public IEventSchemaFormatter GetFormatter(EventSchema schema)
    {
        return schema switch
        {
            EventSchema.EventGridSchema => eventGridFormatter,
            EventSchema.CloudEventV1_0 => cloudEventFormatter,
            _ => throw new ArgumentOutOfRangeException(
                nameof(schema),
                schema,
                "Unknown event schema"
            ),
        };
    }
}
