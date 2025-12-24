using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Wrapper for dead-lettered events with metadata per Azure Event Grid spec.
/// </summary>
public class DeadLetterEvent
{
    /// <summary>
    /// Gets or sets the reason the event was dead-lettered.
    /// Values: MaxDeliveryAttemptsExceeded, EventTimeToLiveExpired, ImmediateDeadLetter_HttpStatus{code}
    /// </summary>
    [JsonPropertyName("deadLetterReason")]
    public required string DeadLetterReason { get; init; }

    /// <summary>
    /// Gets or sets the number of delivery attempts made.
    /// </summary>
    [JsonPropertyName("deliveryAttempts")]
    public int DeliveryAttempts { get; init; }

    /// <summary>
    /// Gets or sets the outcome of the last delivery attempt, if any.
    /// </summary>
    [JsonPropertyName("lastDeliveryOutcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastDeliveryOutcome { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code of the last attempt, if applicable.
    /// </summary>
    [JsonPropertyName("lastHttpStatusCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LastHttpStatusCode { get; init; }

    /// <summary>
    /// Gets or sets the error message from the last attempt, if any.
    /// </summary>
    [JsonPropertyName("lastErrorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the time the event was originally published.
    /// </summary>
    [JsonPropertyName("publishTime")]
    public DateTimeOffset PublishTime { get; init; }

    /// <summary>
    /// Gets or sets the time of the last delivery attempt, if any.
    /// </summary>
    [JsonPropertyName("lastDeliveryAttemptTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastDeliveryAttemptTime { get; init; }

    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    [JsonPropertyName("topicName")]
    public required string TopicName { get; init; }

    /// <summary>
    /// Gets or sets the subscriber name.
    /// </summary>
    [JsonPropertyName("subscriberName")]
    public required string SubscriberName { get; init; }

    /// <summary>
    /// Gets or sets the subscriber type (http, serviceBus, storageQueue).
    /// </summary>
    [JsonPropertyName("subscriberType")]
    public required string SubscriberType { get; init; }

    /// <summary>
    /// Gets or sets the original event payload.
    /// </summary>
    [JsonPropertyName("event")]
    public required object Event { get; init; }
}
