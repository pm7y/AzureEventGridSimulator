using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Properties of a CloudEvents v1.0 event.
/// </summary>
[DataContract]
public class CloudEvent
{
    /// <summary>
    /// Gets or sets the CloudEvents specification version (required).
    /// Must be "1.0".
    /// </summary>
    [DataMember(Name = "specversion")]
    [JsonProperty(PropertyName = "specversion")]
    public string SpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the event type (required).
    /// </summary>
    [DataMember(Name = "type")]
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the event source URI (required).
    /// </summary>
    [DataMember(Name = "source")]
    [JsonProperty(PropertyName = "source")]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the unique event identifier (required).
    /// </summary>
    [DataMember(Name = "id")]
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp in RFC 3339 format (optional).
    /// </summary>
    [DataMember(Name = "time")]
    [JsonProperty(PropertyName = "time", NullValueHandling = NullValueHandling.Ignore)]
    public string Time { get; set; }

    /// <summary>
    /// Gets or sets the subject of the event (optional).
    /// </summary>
    [DataMember(Name = "subject")]
    [JsonProperty(PropertyName = "subject", NullValueHandling = NullValueHandling.Ignore)]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the content type of the data attribute (optional).
    /// </summary>
    [DataMember(Name = "datacontenttype")]
    [JsonProperty(PropertyName = "datacontenttype", NullValueHandling = NullValueHandling.Ignore)]
    public string DataContentType { get; set; }

    /// <summary>
    /// Gets or sets a URI reference to the schema for the data attribute (optional).
    /// </summary>
    [DataMember(Name = "dataschema")]
    [JsonProperty(PropertyName = "dataschema", NullValueHandling = NullValueHandling.Ignore)]
    public string DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the event payload (optional).
    /// </summary>
    [DataMember(Name = "data")]
    [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
    public object Data { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded binary event payload (optional).
    /// Used as an alternative to data for binary content.
    /// </summary>
    [DataMember(Name = "data_base64")]
    [JsonProperty(PropertyName = "data_base64", NullValueHandling = NullValueHandling.Ignore)]
    public string DataBase64 { get; set; }

    [JsonIgnore]
    private DateTime? TimeParsed => string.IsNullOrEmpty(Time) ? null : DateTime.Parse(Time);

    [JsonIgnore]
    private bool TimeIsValid => string.IsNullOrEmpty(Time) || DateTime.TryParse(Time, out _);

    /// <summary>
    /// Validate the CloudEvent according to the CloudEvents v1.0 specification.
    /// </summary>
    /// <exception cref="InvalidOperationException">
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

        // Required: source
        if (string.IsNullOrWhiteSpace(Source))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(Source)}' was not set."
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

            if (TimeParsed.HasValue && TimeParsed.Value.Kind == DateTimeKind.Unspecified)
            {
                throw new InvalidOperationException(
                    $"Property '{nameof(Time)}' must include timezone information."
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
