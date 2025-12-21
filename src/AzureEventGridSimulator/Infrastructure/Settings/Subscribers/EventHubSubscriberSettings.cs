using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for Azure Event Hub subscribers.
/// </summary>
public class EventHubSubscriberSettings : ISubscriberSettings
{
    /// <summary>
    /// Internal reference to the parent topic for connection string inheritance.
    /// Set during validation in SimulatorSettings.
    /// </summary>
    [JsonIgnore]
    internal TopicSettings ParentTopic { get; set; }

    /// <summary>
    /// Gets or sets the Event Hub connection string.
    /// Either this OR (Namespace + SharedAccessKeyName + SharedAccessKey) must be provided.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Event Hub namespace (without .servicebus.windows.net suffix).
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; }

    /// <summary>
    /// Gets or sets the shared access key name.
    /// </summary>
    [JsonPropertyName("sharedAccessKeyName")]
    public string SharedAccessKeyName { get; set; }

    /// <summary>
    /// Gets or sets the shared access key.
    /// </summary>
    [JsonPropertyName("sharedAccessKey")]
    public string SharedAccessKey { get; set; }

    /// <summary>
    /// Gets or sets the Event Hub name.
    /// </summary>
    [JsonPropertyName("eventHubName")]
    public string EventHubName { get; set; }

    /// <summary>
    /// Gets or sets the delivery properties to add to Event Hub messages.
    /// Keys are property names, values specify whether the property is static or dynamic.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, DeliveryPropertySettings> Properties { get; set; }

    /// <summary>
    /// Gets the connection string, either directly specified, built from components, or inherited from
    /// topic.
    /// Falls back to topic-level defaults if not specified at subscriber level.
    /// </summary>
    [JsonIgnore]
    public string EffectiveConnectionString
    {
        get
        {
            // Subscriber-level connection string (direct)
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString;
            }

            // Subscriber-level namespace components
            if (HasSubscriberNamespaceCredentials())
            {
                return BuildConnectionString(Namespace, SharedAccessKeyName, SharedAccessKey);
            }

            // Fall back to topic-level connection string
            if (
                ParentTopic != null
                && !string.IsNullOrWhiteSpace(ParentTopic.EventHubConnectionString)
            )
            {
                return ParentTopic.EventHubConnectionString;
            }

            // Fall back to topic-level namespace components
            if (HasTopicNamespaceCredentials())
            {
                if (ParentTopic != null)
                {
                    return BuildConnectionString(
                        ParentTopic.EventHubNamespace,
                        ParentTopic.EventHubSharedAccessKeyName,
                        ParentTopic.EventHubSharedAccessKey
                    );
                }
            }

            // No connection string available - will fail validation
            return null;
        }
    }

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
    public string SubscriberType => "eventHub";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }

        // Validate Event Hub name
        if (string.IsNullOrWhiteSpace(EventHubName))
        {
            throw new ArgumentException(
                $"Event Hub subscriber '{Name}' must specify an eventHubName."
            );
        }

        // Validate authentication (considering topic-level defaults)
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasTopicConnectionString =
            ParentTopic != null && !string.IsNullOrWhiteSpace(ParentTopic.EventHubConnectionString);

        // Check if subscriber specifies both connection string and any namespace components
        if (hasConnectionString && HasAnySubscriberNamespaceCredential())
        {
            throw new ArgumentException(
                $"Event Hub subscriber '{Name}' should specify either connectionString or namespace credentials, not both."
            );
        }

        // Check if at least one authentication method is available (subscriber or topic level)
        if (
            !hasConnectionString
            && !HasSubscriberNamespaceCredentials()
            && !hasTopicConnectionString
            && !HasTopicNamespaceCredentials()
        )
        {
            throw new ArgumentException(
                $"Event Hub subscriber '{Name}' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey, either at subscriber or topic level."
            );
        }

        // Validate properties
        if (Properties != null)
        {
            foreach (var (propertyName, propertySetting) in Properties)
            {
                propertySetting.Validate(propertyName);
            }
        }

        Filter?.Validate();
        RetryPolicy?.Validate();
        DeadLetter?.Validate();
    }

    private bool HasSubscriberNamespaceCredentials()
    {
        return !string.IsNullOrWhiteSpace(Namespace)
            && !string.IsNullOrWhiteSpace(SharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(SharedAccessKey);
    }

    private bool HasAnySubscriberNamespaceCredential()
    {
        return !string.IsNullOrWhiteSpace(Namespace)
            || !string.IsNullOrWhiteSpace(SharedAccessKeyName)
            || !string.IsNullOrWhiteSpace(SharedAccessKey);
    }

    private bool HasTopicNamespaceCredentials()
    {
        return ParentTopic != null
            && !string.IsNullOrWhiteSpace(ParentTopic.EventHubNamespace)
            && !string.IsNullOrWhiteSpace(ParentTopic.EventHubSharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(ParentTopic.EventHubSharedAccessKey);
    }

    private static string BuildConnectionString(
        string eventHubNamespace,
        string sharedAccessKeyName,
        string sharedAccessKey
    )
    {
        return $"Endpoint=sb://{eventHubNamespace}.servicebus.windows.net/;SharedAccessKeyName={sharedAccessKeyName};SharedAccessKey={sharedAccessKey}";
    }
}
