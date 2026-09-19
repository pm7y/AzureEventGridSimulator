using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Parses events using the CloudEvents v1.0 schema.
///     Supports both binary and structured content modes.
/// </summary>
public class CloudEventSchemaParser(EventSchemaDetector schemaDetector) : IEventSchemaParser
{
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
            return ParseBatchStructuredMode(requestBody);
        }

        // Check if using application/json (strict single-event mode)
        var contentType = context.Request.ContentType;
        var isApplicationJson =
            !string.IsNullOrEmpty(contentType)
            && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("cloudevents", StringComparison.OrdinalIgnoreCase);

        return ParseStructuredMode(requestBody, isApplicationJson);
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
            ExtensionAttributes = GetExtensionAttributesFromHeaders(headers),
        };

        // Parse the body as data
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            // Try to parse as JSON, otherwise treat as string. Default options on purpose: they
            // reject trailing commas, which JsonSerializerOptionsProvider.Default would accept.
            try
            {
                cloudEvent.Data = JsonSerializer.Deserialize<object>(requestBody);
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
    /// <param name="requestBody">The request body containing the CloudEvent.</param>
    /// <param name="strictSingleEvent">
    ///     When true (application/json content-type), arrays are rejected per Azure behavior.
    ///     When false (application/cloudevents+json), arrays are accepted as single-element batches.
    /// </param>
    private SimulatorEvent[] ParseStructuredMode(string requestBody, bool strictSingleEvent)
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
                        SchemaErrorMessages.NotConforming(Schema)
                            + " Token Expected: StartObject, Actual Token Received: StartArray."
                    );
                }

                if (document.RootElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(SchemaErrorMessages.NotConforming(Schema));
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
            throw new InvalidOperationException(FormatJsonError(ex.Message), ex);
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
    private SimulatorEvent[] ParseBatchStructuredMode(string requestBody)
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
            throw new InvalidOperationException(FormatJsonError(ex.Message), ex);
        }

        if (events == null || events.Length == 0)
        {
            throw new InvalidOperationException(SchemaErrorMessages.NotConforming(Schema));
        }

        return events.Select(SimulatorEvent.FromCloudEvent).ToArray();
    }

    // CloudEvents binary-mode headers that map to known attributes; any other "ce-" header
    // is an extension context attribute and must be preserved.
    private static readonly HashSet<string> ReservedCeHeaders = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        Constants.CeSpecVersionHeader,
        Constants.CeTypeHeader,
        Constants.CeSourceHeader,
        Constants.CeIdHeader,
        Constants.CeTimeHeader,
        Constants.CeSubjectHeader,
        Constants.CeDataContentTypeHeader,
        Constants.CeDataSchemaHeader,
    };

    private const string CeHeaderPrefix = "ce-";

    /// <summary>
    ///     Collects any "ce-" headers that are not known CloudEvents attributes into a dictionary of
    ///     extension context attributes, keyed by the attribute name (the header name without "ce-").
    /// </summary>
    private static Dictionary<string, JsonElement>? GetExtensionAttributesFromHeaders(
        IHeaderDictionary headers
    )
    {
        Dictionary<string, JsonElement>? extensionAttributes = null;

        foreach (var header in headers)
        {
            if (
                !header.Key.StartsWith(CeHeaderPrefix, StringComparison.OrdinalIgnoreCase)
                || ReservedCeHeaders.Contains(header.Key)
            )
            {
                continue;
            }

            // Preserve present-but-empty extension headers (e.g. "ce-foo: ") rather than dropping
            // them, matching GetHeaderValue. Only skip when the header carries no value at all.
            var value = header.Value.FirstOrDefault();
            if (value is null)
            {
                continue;
            }

            var name = header.Key[CeHeaderPrefix.Length..];
            extensionAttributes ??= new Dictionary<string, JsonElement>(
                StringComparer.OrdinalIgnoreCase
            );
            extensionAttributes[name] = JsonSerializer.SerializeToElement(DecodeHeaderValue(value));
        }

        return extensionAttributes;
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
    /// <exception cref="EventParseException">
    ///     The header is missing or empty (error code InvalidCloudEventHeader).
    /// </exception>
    private static string GetRequiredHeaderValue(IHeaderDictionary headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var values))
        {
            throw new EventParseException(
                $"{headerName} header is missing for the cloud event. "
                    + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes",
                ErrorDetailCodes.InvalidCloudEventHeader
            );
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            throw new EventParseException(
                $"{headerName} header is empty for the cloud event. "
                    + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes",
                ErrorDetailCodes.InvalidCloudEventHeader
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

    private static string FormatJsonError(string message)
    {
        return SchemaErrorMessages.FormatMissingPropertiesError(
            message,
            EventSchema.CloudEventV1_0,
            FieldPriority,
            FieldDisplayNames
        );
    }
}
