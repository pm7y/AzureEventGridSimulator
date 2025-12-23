using System.Globalization;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Properties of an event published to an Event Grid topic.
/// </summary>
public class EventGridEvent
{
    /// <summary>
    /// Gets or sets an unique identifier for the event.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets a resource path relative to the topic path.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets event data specific to the event type.
    /// </summary>
    [JsonPropertyName("data")]
    public object Data { get; set; }

    /// <summary>
    /// Gets or sets the type of the event that occurred.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the time (in UTC) the event was generated.
    /// </summary>
    [JsonPropertyName("eventTime")]
    public string EventTime { get; set; }

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
    /// Gets or sets the schema version of the data object.
    /// </summary>
    [JsonPropertyName("dataVersion")]
    public string DataVersion { get; set; }

    /// <summary>
    /// Gets the schema version of the event metadata.
    /// </summary>
    [JsonPropertyName("metadataVersion")]
    public string MetadataVersion { get; set; }

    /// <summary>
    /// Gets the resource path of the event source.
    /// This property is set by Event Grid, not by publishers.
    /// </summary>
    [JsonPropertyName("topic")]
    [JsonInclude]
    public string Topic { get; private set; }

    /// <summary>
    /// Indicates whether the Topic has been set by the simulator.
    /// </summary>
    [JsonIgnore]
    internal bool TopicHasBeenSet { get; private set; }

    /// <summary>
    /// Sets the topic path. This should only be called by the simulator.
    /// </summary>
    internal void SetTopic(string topic)
    {
        Topic = topic;
        TopicHasBeenSet = true;
    }

    /// <summary>
    /// Validate the object.
    /// </summary>
    /// <exception cref="InvalidOperationException" >
    /// Thrown if validation fails
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException($"Required property '{nameof(Id)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(Subject)}' was not set."
            );
        }

        if (string.IsNullOrWhiteSpace(EventType))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(EventType)}' was not set."
            );
        }

        if (string.IsNullOrWhiteSpace(EventTime))
        {
            throw new InvalidOperationException(
                $"Required property '{nameof(EventTime)}' was not set."
            );
        }

        if (!EventTimeIsValid)
        {
            throw new InvalidOperationException(
                $"The event time property '{nameof(EventTime)}' was not a valid date/time."
            );
        }

        if (!EventTimeHasTimezone)
        {
            throw new InvalidOperationException(
                $"Property '{nameof(EventTime)}' must include a timezone indicator (e.g., 'Z' for UTC or an offset like '+00:00')."
            );
        }

        if (MetadataVersion != null && MetadataVersion != "1")
        {
            throw new InvalidOperationException(
                $"Property '{nameof(MetadataVersion)}' was found to be set to '{MetadataVersion}', but was expected to either be null or be set to 1."
            );
        }

        // Topic must NOT be set by the publisher - Event Grid sets this automatically
        // Skip this check if the simulator has already set the topic via SetTopic()
        if (!TopicHasBeenSet && !string.IsNullOrEmpty(Topic))
        {
            throw new InvalidOperationException(
                $"Property '{nameof(Topic)}' was found to be set to '{Topic}', but was expected to either be null/empty."
            );
        }
    }
}
