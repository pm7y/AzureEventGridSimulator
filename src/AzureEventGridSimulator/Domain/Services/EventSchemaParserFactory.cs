using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Factory for creating event schema parsers based on the detected schema.
/// </summary>
public class EventSchemaParserFactory(
    EventGridSchemaParser eventGridParser,
    CloudEventSchemaParser cloudEventParser
)
{
    /// <summary>
    ///     Gets the appropriate parser for the specified schema.
    /// </summary>
    /// <param name="schema">
    ///     The event schema.
    /// </param>
    /// <returns>
    ///     The parser for the schema.
    /// </returns>
    public IEventSchemaParser GetParser(EventSchema schema)
    {
        return schema switch
        {
            EventSchema.EventGridSchema => eventGridParser,
            EventSchema.CloudEventV1_0 => cloudEventParser,
            _ => throw new ArgumentOutOfRangeException(
                nameof(schema),
                schema,
                "Unknown event schema"
            ),
        };
    }
}
