using System.Globalization;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Properties of a CloudEvents v1.0 event.
/// </summary>
public class CloudEvent
{
    /// <summary>
    /// Gets or sets the CloudEvents specification version (required).
    /// Must be "1.0".
    /// </summary>
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the event type (required).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the event source URI (required).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the unique event identifier (required).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp in RFC 3339 format (optional).
    /// </summary>
    [JsonPropertyName("time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Time { get; set; }

    /// <summary>
    /// Gets or sets the subject of the event (optional).
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the content type of the data attribute (optional).
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DataContentType { get; set; }

    /// <summary>
    /// Gets or sets a URI reference to the schema for the data attribute (optional).
    /// </summary>
    [JsonPropertyName("dataschema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the event payload (optional).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Data { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded binary event payload (optional).
    /// Used as an alternative to data for binary content.
    /// </summary>
    [JsonPropertyName("data_base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DataBase64 { get; set; }

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
    /// </summary>
    /// <exception cref="InvalidOperationException" >
    /// Thrown if validation fails
    /// </exception>
    public void Validate()
    {
        // Required: specversion
        if (string.IsNullOrWhiteSpace(SpecVersion))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(SpecVersion)}' was not set."
            );
        }

        if (SpecVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Property '{nameof(SpecVersion)}' must be '1.0', but was '{SpecVersion}'."
            );
        }

        // Required: type
        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new InvalidOperationException($"Required property '{nameof(Type)}' was not set.");
        }

        // Required: source - must be a non-empty URI-reference per CloudEvents spec
        if (string.IsNullOrWhiteSpace(Source))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(Source)}' was not set."
            );
        }

        if (!Uri.TryCreate(Source, UriKind.RelativeOrAbsolute, out _))
        {
            throw new InvalidOperationException(
                $"Property '{nameof(Source)}' must be a valid URI-reference."
            );
        }

        // Required: id
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException($"Required property '{nameof(Id)}' was not set.");
        }

        // Optional: time - if present, must be valid RFC 3339
        if (!string.IsNullOrEmpty(Time))
        {
            if (!TimeIsValid)
            {
                throw new InvalidOperationException(
                    $"Property '{nameof(Time)}' was not a valid RFC 3339 timestamp."
                );
            }

            if (!TimeHasTimezone)
            {
                throw new InvalidOperationException(
                    $"Property '{nameof(Time)}' must include timezone information (e.g., 'Z' for UTC or an offset like '+00:00')."
                );
            }
        }

        // Optional: dataschema - if present, must be a valid URI
        if (!string.IsNullOrEmpty(DataSchema))
        {
            if (!Uri.TryCreate(DataSchema, UriKind.RelativeOrAbsolute, out _))
            {
                throw new InvalidOperationException(
                    $"Property '{nameof(DataSchema)}' must be a valid URI."
                );
            }
        }

        // data and data_base64 are mutually exclusive
        if (Data != null && !string.IsNullOrEmpty(DataBase64))
        {
            throw new InvalidOperationException(
                "Properties 'data' and 'data_base64' are mutually exclusive."
            );
        }
    }
}
