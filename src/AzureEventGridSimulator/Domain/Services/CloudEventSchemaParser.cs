using System.Text.Json;
using System.Text.RegularExpressions;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Parses events using the CloudEvents v1.0 schema.
///     Supports both binary and structured content modes.
/// </summary>
public partial class CloudEventSchemaParser(EventSchemaDetector schemaDetector) : IEventSchemaParser
{
    // Matches System.Text.Json missing required properties error
    [GeneratedRegex(
        @"missing required properties.*including:\s*(?<props>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking
    )]
    private static partial Regex MissingPropertiesRegex();

    private const string SchemaName = "CloudEventV10";

    /// <inheritdoc />
    public EventSchema Schema => EventSchema.CloudEventV1_0;

    /// <inheritdoc />
    public SimulatorEvent[] Parse(HttpContext context, string requestBody)
    {
        if (schemaDetector.IsBinaryMode(context))
        {
            return ParseBinaryMode(context, requestBody);
        }

        if (schemaDetector.IsBatchMode(context))
        {
            return ParseBatchStructuredMode(context, requestBody);
        }

        // Check if using application/json (strict single-event mode)
        var contentType = context.Request.ContentType;
        var isApplicationJson =
            !string.IsNullOrEmpty(contentType)
            && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("cloudevents", StringComparison.OrdinalIgnoreCase);

        return ParseStructuredMode(context, requestBody, isApplicationJson);
    }

    /// <inheritdoc />
    public void Validate(SimulatorEvent[] events)
    {
        foreach (var evt in events)
        {
            evt.Validate();
        }
    }

    /// <summary>
    ///     Parses a CloudEvent from binary content mode.
    ///     In binary mode, CloudEvents attributes are in HTTP headers.
    /// </summary>
    private SimulatorEvent[] ParseBinaryMode(HttpContext context, string requestBody)
    {
        var headers = context.Request.Headers;

        var cloudEvent = new CloudEvent
        {
            SpecVersion = GetRequiredHeaderValue(headers, Constants.CeSpecVersionHeader),
            Id = GetRequiredHeaderValue(headers, Constants.CeIdHeader),
            Source = GetRequiredHeaderValue(headers, Constants.CeSourceHeader),
            Type = GetRequiredHeaderValue(headers, Constants.CeTypeHeader),
            Time = GetHeaderValue(headers, Constants.CeTimeHeader),
            Subject = GetHeaderValue(headers, Constants.CeSubjectHeader),
            DataContentType =
                GetHeaderValue(headers, Constants.CeDataContentTypeHeader)
                ?? context.Request.ContentType,
            DataSchema = GetHeaderValue(headers, Constants.CeDataSchemaHeader),
        };

        // Parse the body as data
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            // Try to parse as JSON, otherwise treat as string
            try
            {
                using var doc = JsonDocument.Parse(requestBody);
                cloudEvent.Data = JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
            }
            catch (JsonException)
            {
                cloudEvent.Data = requestBody;
            }
        }

        return [SimulatorEvent.FromCloudEvent(cloudEvent)];
    }

    /// <summary>
    ///     Parses a single CloudEvent from structured content mode.
    ///     In structured mode, all CloudEvents attributes are in the JSON body.
    /// </summary>
    /// <param name="context">The HTTP context for the request.</param>
    /// <param name="requestBody">The request body containing the CloudEvent.</param>
    /// <param name="strictSingleEvent">
    ///     When true (application/json content-type), arrays are rejected per Azure behavior.
    ///     When false (application/cloudevents+json), arrays are accepted as single-element batches.
    /// </param>
    private SimulatorEvent[] ParseStructuredMode(
        HttpContext context,
        string requestBody,
        bool strictSingleEvent
    )
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("Unexpected end when reading JSON.");
        }

        CloudEvent? cloudEvent;

        try
        {
            using var document = JsonDocument.Parse(requestBody);

            // Check if it's an array
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Azure behavior: application/json expects a single object, not an array
                if (strictSingleEvent)
                {
                    throw new InvalidOperationException(
                        $"This resource is configured to receive event in '{SchemaName}' schema. "
                            + "The JSON received does not conform to the expected schema. "
                            + $"Token Expected: StartObject, Actual Token Received: StartArray.{context.GenerateReportSuffix()}"
                    );
                }

                if (document.RootElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(
                        $"This resource is configured to receive event in '{SchemaName}' schema. "
                            + "The JSON received does not conform to the expected schema."
                    );
                }

                // Handle single event in array format (for application/cloudevents+json)
                var events = JsonSerializer.Deserialize<CloudEvent[]>(
                    requestBody,
                    JsonSerializerOptionsProvider.Default
                );
                return events?.Select(SimulatorEvent.FromCloudEvent).ToArray() ?? [];
            }

            cloudEvent = JsonSerializer.Deserialize<CloudEvent>(
                requestBody,
                JsonSerializerOptionsProvider.Default
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(FormatJsonError(ex.Message, context), ex);
        }

        if (cloudEvent == null)
        {
            throw new InvalidOperationException("Failed to parse CloudEvent from request body.");
        }

        return [SimulatorEvent.FromCloudEvent(cloudEvent)];
    }

    /// <summary>
    ///     Parses multiple CloudEvents from batch structured content mode.
    /// </summary>
    private SimulatorEvent[] ParseBatchStructuredMode(HttpContext context, string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("Unexpected end when reading JSON.");
        }

        CloudEvent[]? events;

        try
        {
            events = JsonSerializer.Deserialize<CloudEvent[]>(
                requestBody,
                JsonSerializerOptionsProvider.Default
            );
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

        return events.Select(SimulatorEvent.FromCloudEvent).ToArray();
    }

    /// <summary>
    ///     Gets and decodes a header value, handling percent-encoding per CloudEvents HTTP binding spec.
    /// </summary>
    private static string? GetHeaderValue(IHeaderDictionary headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return DecodeHeaderValue(value);
    }

    /// <summary>
    ///     Gets and decodes a required header value.
    ///     Throws if the header is missing or empty.
    /// </summary>
    private static string GetRequiredHeaderValue(IHeaderDictionary headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var values))
        {
            throw new InvalidOperationException(
                $"{headerName} header is missing for the cloud event. "
                    + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes"
            );
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"{headerName} header is empty for the cloud event. "
                    + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes"
            );
        }

        return DecodeHeaderValue(value);
    }

    /// <summary>
    ///     Decodes a header value, handling percent-encoding per CloudEvents HTTP binding spec.
    /// </summary>
    private static string DecodeHeaderValue(string value)
    {
        // CloudEvents HTTP Protocol Binding requires percent-encoding for certain characters
        // in header values (spaces, non-ASCII, etc.). We need to decode them.
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            // If decoding fails, return the original value
            return value;
        }
    }

    // Azure validates fields in this order for CloudEvents
    // Note: Azure displays 'type' as 'eventType' in error messages
    private static readonly string[] FieldPriority = ["specversion", "type", "source", "id"];

    // Map CloudEvents field names to Azure's display names
    private static readonly Dictionary<string, string> FieldDisplayNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["type"] = "eventType",
    };

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
                // Use Azure's display name if available (e.g., 'type' -> 'eventType')
                var displayName = FieldDisplayNames.GetValueOrDefault(firstProperty, firstProperty);
                return $"This resource is configured for '{SchemaName}' schema and requires '{displayName}' property to be set.{context.GenerateReportSuffix()}";
            }
        }

        // Return original message for other JSON errors
        return message;
    }
}
