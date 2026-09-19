using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Parses events using the Azure Event Grid schema.
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
            throw new InvalidOperationException("Unexpected end when reading JSON.");
        }

        EventGridEvent[]? events;

        try
        {
            using var document = JsonDocument.Parse(requestBody);

            // Azure accepts both single object and array format for EventGrid schema
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var singleEvent = JsonSerializer.Deserialize<EventGridEvent>(
                    requestBody,
                    JsonSerializerOptionsProvider.Default
                );
                events = singleEvent != null ? [singleEvent] : null;
            }
            else
            {
                events = JsonSerializer.Deserialize<EventGridEvent[]>(
                    requestBody,
                    JsonSerializerOptionsProvider.Default
                );
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                SchemaErrorMessages.FormatMissingPropertiesError(ex.Message, Schema, FieldPriority),
                ex
            );
        }

        if (events == null || events.Length == 0)
        {
            throw new InvalidOperationException(SchemaErrorMessages.NotConforming(Schema));
        }

        return [.. events.Select(SimulatorEvent.FromEventGridEvent)];
    }

    /// <inheritdoc />
    public void Validate(SimulatorEvent[] events)
    {
        foreach (var evt in events)
        {
            evt.Validate();
        }
    }

    // Azure validates fields in this order (observed from real Azure responses)
    private static readonly string[] FieldPriority = ["subject", "id", "eventType", "eventTime"];
}
