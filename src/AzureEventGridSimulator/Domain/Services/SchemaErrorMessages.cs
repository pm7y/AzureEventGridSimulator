using System.Text.RegularExpressions;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Builds the Azure-style error messages that the schema parsers return. The HTTP report
///     suffix is not part of these messages: EventGridMiddleware appends it to the response.
/// </summary>
internal static partial class SchemaErrorMessages
{
    // Matches System.Text.Json missing required properties error
    [GeneratedRegex(
        @"missing required properties.*including:\s*(?<props>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking
    )]
    private static partial Regex MissingPropertiesRegex();

    /// <summary>
    ///     The message for a body that doesn't have the shape the schema expects, such as an empty
    ///     array.
    /// </summary>
    public static string NotConforming(EventSchema schema)
    {
        return $"This resource is configured to receive event in '{schema.ToAzureSchemaName()}' schema. "
            + "The JSON received does not conform to the expected schema.";
    }

    /// <summary>
    ///     The message for an event that is missing a required property.
    /// </summary>
    public static string RequiresProperty(EventSchema schema, string property)
    {
        return $"This resource is configured for '{schema.ToAzureSchemaName()}' schema and requires '{property}' property to be set.";
    }

    /// <summary>
    ///     Rewrites System.Text.Json's missing required properties error as Azure's message, which
    ///     names only the first missing property in the order Azure validates them. Any other JSON
    ///     error message is returned unchanged.
    /// </summary>
    /// <param name="message">The JsonException message.</param>
    /// <param name="schema">The schema being parsed.</param>
    /// <param name="fieldPriority">The JSON property names, in the order Azure validates them.</param>
    /// <param name="displayNames">
    ///     The names Azure shows for some properties, keyed by JSON property name.
    /// </param>
    public static string FormatMissingPropertiesError(
        string message,
        EventSchema schema,
        IReadOnlyList<string> fieldPriority,
        IReadOnlyDictionary<string, string>? displayNames = null
    )
    {
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
                fieldPriority.FirstOrDefault(f => missingProps.Contains(f))
                ?? missingProps.FirstOrDefault();

            if (!string.IsNullOrEmpty(firstProperty))
            {
                // Use Azure's display name if there is one (e.g. 'type' -> 'eventType')
                var displayName = displayNames is null
                    ? firstProperty
                    : displayNames.GetValueOrDefault(firstProperty, firstProperty);
                return RequiresProperty(schema, displayName);
            }
        }

        // Return original message for other JSON errors
        return message;
    }
}
