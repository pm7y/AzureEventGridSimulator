using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for Azure Storage Queue subscribers.
/// </summary>
public class StorageQueueSubscriberSettings : ISubscriberSettings
{
    /// <summary>
    /// Internal reference to the parent topic for connection string inheritance.
    /// Set during validation in SimulatorSettings.
    /// </summary>
    [JsonIgnore]
    internal TopicSettings ParentTopic { get; set; }

    /// <summary>
    /// Gets or sets the Storage Queue connection string.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    [JsonPropertyName("queueName")]
    public string QueueName { get; set; }

    /// <summary>
    /// Gets the effective connection string, either from subscriber or topic level.
    /// </summary>
    [JsonIgnore]
    public string EffectiveConnectionString =>
        !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            : ParentTopic?.StorageQueueConnectionString;

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("filter")]
    public FilterSetting Filter { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; set; }

    /// <summary>
    /// Gets or sets the retry policy for this subscriber.
    /// If null, default Azure Event Grid retry behavior is used (enabled with 30 attempts, 24h TTL).
    /// </summary>
    [JsonPropertyName("retryPolicy")]
    public RetryPolicySettings RetryPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets the dead-letter settings for this subscriber.
    /// Events that cannot be delivered are written to the dead-letter destination.
    /// </summary>
    [JsonPropertyName("deadLetter")]
    public DeadLetterSettings DeadLetter { get; set; } = new();

    [JsonIgnore]
    public string SubscriberType => "storageQueue";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }

        // Validate connection string (considering topic-level default)
        if (string.IsNullOrWhiteSpace(EffectiveConnectionString))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a connectionString, either at subscriber or topic level."
            );
        }

        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a queueName."
            );
        }

        Filter?.Validate();
        RetryPolicy?.Validate();
        DeadLetter?.Validate();
    }
}
