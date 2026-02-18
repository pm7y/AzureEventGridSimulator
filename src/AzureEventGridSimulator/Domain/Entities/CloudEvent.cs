using System.Globalization;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     Properties of a CloudEvents v1.0 event.
/// </summary>
public class CloudEvent
{
    private const string SchemaName = "CloudEventV10";

    /// <summary>
    ///     Gets or sets the CloudEvents specification version.
    ///     Note: Azure Event Grid is lenient and accepts events without this field.
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("specversion")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public string? SpecVersion { get; set; }

    /// <summary>
    ///     Gets or sets the event type (required).
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public required string Type { get; set; }

    /// <summary>
    ///     Gets or sets the event source URI.
    ///     Note: Azure Event Grid is lenient and accepts events without this field.
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("source")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public string? Source { get; set; }

    /// <summary>
    ///     Gets or sets the unique event identifier (required).
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the event timestamp in RFC 3339 format (optional).
    /// </summary>
    [JsonPropertyName("time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Time { get; set; }

    /// <summary>
    ///     Gets or sets the subject of the event (optional).
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subject { get; set; }

    /// <summary>
    ///     Gets or sets the content type of the data attribute (optional).
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataContentType { get; set; }

    /// <summary>
    ///     Gets or sets a URI reference to the schema for the data attribute (optional).
    /// </summary>
    [JsonPropertyName("dataschema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataSchema { get; set; }

    /// <summary>
    ///     Gets or sets the event payload (optional).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    /// <summary>
    ///     Gets or sets the base64-encoded binary event payload (optional).
    ///     Used as an alternative to data for binary content.
    /// </summary>
    [JsonPropertyName("data_base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataBase64 { get; set; }

    [JsonIgnore]
    private DateTimeOffset? TimeParsed =>
        string.IsNullOrEmpty(Time)
            ? null
            : DateTimeOffset.Parse(Time, CultureInfo.InvariantCulture);

    [JsonIgnore]
    private bool TimeIsValid =>
        string.IsNullOrEmpty(Time)
        || DateTimeOffset.TryParse(Time, CultureInfo.InvariantCulture, out _);

    [JsonIgnore]
    private bool TimeHasTimezone =>
        string.IsNullOrEmpty(Time)
        || Time.Contains('Z')
        || Time.Contains('+')
        || (Time.Length > 10 && Time[10..].Contains('-'));

    /// <summary>
    ///     Validate the CloudEvent according to Azure Event Grid's lenient behavior.
    ///     Note: Azure is very lenient - it accepts events without specversion, source,
    ///     doesn't enforce field length limits, accepts any specversion value, and
    ///     coerces types (numbers/booleans to strings).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if validation fails
    /// </exception>
    public void Validate()
    {
        // Azure is lenient - it accepts any specversion value including non-"1.0"
        // Azure is lenient - it accepts events without source

        // Validate required fields are non-empty
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'id' property to be set."
            );
        }

        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'eventType' property to be set."
            );
        }

        // Azure does NOT enforce field length limits for type or subject

        // Optional: time - if present, must be valid date/time
        // Note: Azure is lenient and accepts time without timezone (though CloudEvents spec recommends RFC 3339 with timezone)
        if (!string.IsNullOrEmpty(Time) && !TimeIsValid)
        {
            throw new InvalidOperationException(
                "The event time property 'time' was not a valid date/time."
            );
        }

        // Optional: dataschema - if present, must be a valid URI
        if (!string.IsNullOrEmpty(DataSchema))
        {
            if (!Uri.TryCreate(DataSchema, UriKind.RelativeOrAbsolute, out _))
            {
                throw new InvalidOperationException(
                    $"This resource is configured for '{SchemaName}' schema and requires 'dataschema' property to be a valid URI."
                );
            }
        }

        // Note: Azure Event Grid is lenient and accepts both data and data_base64
        // The spec says they are mutually exclusive, but Azure doesn't enforce this
    }
}
