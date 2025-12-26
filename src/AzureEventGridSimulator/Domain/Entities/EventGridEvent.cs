using System.Globalization;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Infrastructure.JsonConverters;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     Properties of an event published to an Event Grid topic.
/// </summary>
public class EventGridEvent
{
    private const string SchemaName = "EventGridEvent";

    /// <summary>
    ///     Gets or sets an unique identifier for the event (required).
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets a resource path relative to the topic path (required).
    /// </summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }

    /// <summary>
    ///     Gets or sets event data specific to the event type (optional).
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>
    ///     Gets or sets the type of the event that occurred (required).
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("eventType")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public required string EventType { get; set; }

    /// <summary>
    ///     Gets or sets the time (in UTC) the event was generated (required).
    /// </summary>
    [JsonPropertyName("eventTime")]
    public required string EventTime { get; set; }

    [JsonIgnore]
    private DateTimeOffset EventTimeParsed =>
        DateTimeOffset.Parse(EventTime, CultureInfo.InvariantCulture);

    [JsonIgnore]
    private bool EventTimeIsValid =>
        DateTimeOffset.TryParse(EventTime, CultureInfo.InvariantCulture, out _);

    [JsonIgnore]
    private bool EventTimeHasTimezone =>
        EventTime.Contains('Z')
        || EventTime.Contains('+')
        || (EventTime.Length > 10 && EventTime[10..].Contains('-'));

    /// <summary>
    ///     Gets or sets the schema version of the data object.
    ///     Azure is lenient and coerces numbers/booleans to strings.
    /// </summary>
    [JsonPropertyName("dataVersion")]
    [JsonConverter(typeof(StringOrPrimitiveConverter))]
    public string? DataVersion { get; set; }

    /// <summary>
    ///     Gets the schema version of the event metadata.
    /// </summary>
    [JsonPropertyName("metadataVersion")]
    public string? MetadataVersion { get; set; }

    /// <summary>
    ///     Gets the resource path of the event source.
    ///     This property is set by Event Grid, not by publishers.
    /// </summary>
    [JsonPropertyName("topic")]
    [JsonInclude]
    public string? Topic { get; private set; }

    /// <summary>
    ///     Indicates whether the Topic has been set by the simulator.
    /// </summary>
    [JsonIgnore]
    internal bool TopicHasBeenSet { get; private set; }

    /// <summary>
    ///     Sets the topic path. This should only be called by the simulator.
    /// </summary>
    internal void SetTopic(string topic)
    {
        Topic = topic;
        TopicHasBeenSet = true;
    }

    /// <summary>
    ///     Validate the object according to Azure Event Grid's lenient behavior.
    ///     Note: Azure does NOT enforce field length limits.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if validation fails
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'id' property to be set."
            );

        if (string.IsNullOrWhiteSpace(Subject))
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'subject' property to be set."
            );

        // Azure does NOT enforce subject length limits

        if (string.IsNullOrWhiteSpace(EventType))
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'eventType' property to be set."
            );

        // Azure does NOT enforce eventType length limits

        // DataVersion is optional, but if provided it must be non-empty
        if (DataVersion != null && string.IsNullOrWhiteSpace(DataVersion))
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'dataVersion' property to be set."
            );

        if (!EventTimeIsValid)
            throw new InvalidOperationException(
                $"This resource is configured for '{SchemaName}' schema and requires 'eventTime' property to be a valid RFC 3339 timestamp."
            );

        // Note: Azure is lenient and accepts eventTime without timezone (though best practice is to include it)

        if (MetadataVersion != null && MetadataVersion != "1")
            throw new InvalidOperationException(
                $"Property 'metadataVersion' was found to be set to {MetadataVersion}, but was expected to either be null or be set to 1."
            );

        // Topic must NOT be set by the publisher - Event Grid sets this automatically
        // Skip this check if the simulator has already set the topic via SetTopic()
        // Azure returns 401 when the topic field doesn't match the actual endpoint topic
        if (!TopicHasBeenSet && !string.IsNullOrEmpty(Topic))
            throw new TopicAuthorizationException(
                $"This resource is configured for '{SchemaName}' schema. The 'topic' property must not be set by the publisher."
            );
    }
}
