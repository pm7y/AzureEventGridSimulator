using System.Text.Json;
using System.Text.RegularExpressions;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Parses events using the Azure Event Grid schema.
/// </summary>
public partial class EventGridSchemaParser : IEventSchemaParser
{
    /// <inheritdoc />
    public EventSchema Schema => EventSchema.EventGridSchema;

    private const string SchemaName = "EventGridEvent";

    // Matches System.Text.Json missing required properties error
    [GeneratedRegex(
        @"missing required properties.*including:\s*(?<props>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking
    )]
    private static partial Regex MissingPropertiesRegex();

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
            throw new InvalidOperationException(FormatJsonError(ex.Message, context), ex);
        }

        if (events == null || events.Length == 0)
        {
            throw new InvalidOperationException(
                $"This resource is configured to receive event in '{SchemaName}' schema. "
                    + "The JSON received does not conform to the expected schema."
            );
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

    // Azure validates fields in this order (observed from real Azure responses)
    private static readonly string[] FieldPriority = ["subject", "id", "eventType", "eventTime"];

    private static string FormatJsonError(string message, HttpContext context)
    {
        // Check for missing required properties pattern
        var match = MissingPropertiesRegex().Match(message);
        if (match.Success)
        {
            // Extract all missing property names
            // System.Text.Json format: "missing required properties including: 'id'."
            var propertiesPart = match.Groups["props"].Value.TrimEnd('.');
            var missingProps = propertiesPart
                .Split(',')
                .Select(p => p.Trim().Trim('\'', '"'))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Pick the first property in Azure's validation order
            var firstProperty =
                FieldPriority.FirstOrDefault(f => missingProps.Contains(f))
                ?? missingProps.FirstOrDefault();

            if (!string.IsNullOrEmpty(firstProperty))
            {
                return $"This resource is configured for '{SchemaName}' schema and requires '{firstProperty}' property to be set.{context.GenerateReportSuffix()}";
            }
        }

        // Return original message for other JSON errors
        return message;
    }
}
