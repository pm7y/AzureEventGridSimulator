using System.Globalization;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Properties of a CloudEvents v1.0 event.
/// </summary>
public class CloudEvent
{
    private const string SchemaName = "CloudEventV10";

    /// <summary>
    /// Gets or sets the CloudEvents specification version (required).
    /// </summary>
    [JsonPropertyName("specversion")]
    public required string SpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the event type (required).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the event source URI (required).
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets the unique event identifier (required).
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp in RFC 3339 format (optional).
    /// </summary>
    [JsonPropertyName("time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Time { get; set; }

    /// <summary>
    /// Gets or sets the subject of the event (optional).
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the content type of the data attribute (optional).
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataContentType { get; set; }

    /// <summary>
    /// Gets or sets a URI reference to the schema for the data attribute (optional).
    /// </summary>
    [JsonPropertyName("dataschema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the event payload (optional).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded binary event payload (optional).
    /// Used as an alternative to data for binary content.
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
    /// Validate the CloudEvent according to the CloudEvents v1.0 specification.
    /// Required properties (SpecVersion, Type, Source, Id) are enforced by the 'required' modifier
    /// for presence, but this method validates they are non-empty and have valid formats.
    /// </summary>
    /// <exception cref="InvalidOperationException" >
    /// Thrown if validation fails
    /// </exception>
    public void Validate()
    {
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

        // Note: Azure Event Grid does not validate specversion or source values - it accepts any value

        // Optional: time - if present, must be valid date/time
        if (!string.IsNullOrEmpty(Time) && (!TimeIsValid || !TimeHasTimezone))
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

        // data and data_base64 are mutually exclusive
        if (Data != null && !string.IsNullOrEmpty(DataBase64))
        {
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema. The 'data' and 'data_base64' properties are mutually exclusive."
            );
        }
    }
}
