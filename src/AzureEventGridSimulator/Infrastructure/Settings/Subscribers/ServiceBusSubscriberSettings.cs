using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for Azure Service Bus subscribers (queues and topics).
/// </summary>
public class ServiceBusSubscriberSettings : ISubscriberSettings
{
    /// <summary>
    ///     Internal reference to the parent topic for connection string inheritance.
    ///     Set during validation in SimulatorSettings.
    /// </summary>
    [JsonIgnore]
    internal TopicSettings? ParentTopic { get; set; }

    /// <summary>
    ///     Gets or sets the Service Bus connection string.
    ///     Either this OR (Namespace + SharedAccessKeyName + SharedAccessKey) must be provided.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; init; }

    /// <summary>
    ///     Gets or sets the Service Bus namespace (without .servicebus.windows.net suffix).
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    /// <summary>
    ///     Gets or sets the shared access key name.
    /// </summary>
    [JsonPropertyName("sharedAccessKeyName")]
    public string? SharedAccessKeyName { get; init; }

    /// <summary>
    ///     Gets or sets the shared access key.
    /// </summary>
    [JsonPropertyName("sharedAccessKey")]
    public string? SharedAccessKey { get; init; }

    /// <summary>
    ///     Gets or sets the topic name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>
    ///     Gets or sets the queue name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("queue")]
    public string? Queue { get; init; }

    /// <summary>
    ///     Gets or sets the delivery properties to add to Service Bus messages.
    ///     Keys are property names, values specify whether the property is static or dynamic.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, DeliveryPropertySettings>? Properties { get; init; }

    /// <summary>
    ///     Gets the destination name (either topic or queue).
    /// </summary>
    [JsonIgnore]
    public string DestinationName =>
        !string.IsNullOrWhiteSpace(Topic)
            ? Topic
            : Queue ?? throw new InvalidOperationException("Neither Topic nor Queue is set");

    /// <summary>
    ///     Gets whether the destination is a topic (vs a queue).
    /// </summary>
    [JsonIgnore]
    public bool IsTopic => !string.IsNullOrWhiteSpace(Topic);

    /// <summary>
    ///     Gets the connection string, either directly specified, built from components, or inherited from
    ///     topic.
    ///     Falls back to topic-level defaults if not specified at subscriber level.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveConnectionString
    {
        get
        {
            // Subscriber-level connection string (direct)
            if (!string.IsNullOrWhiteSpace(ConnectionString))
                return ConnectionString;

            // Subscriber-level namespace components
            if (HasSubscriberNamespaceCredentials())
                return BuildConnectionString(Namespace!, SharedAccessKeyName!, SharedAccessKey!);

            // Fall back to topic-level connection string
            if (
                ParentTopic != null
                && !string.IsNullOrWhiteSpace(ParentTopic.ServiceBusConnectionString)
            )
                return ParentTopic.ServiceBusConnectionString;

            // Fall back to topic-level namespace components
            if (HasTopicNamespaceCredentials())
                if (ParentTopic != null)
                    return BuildConnectionString(
                        ParentTopic.ServiceBusNamespace!,
                        ParentTopic.ServiceBusSharedAccessKeyName!,
                        ParentTopic.ServiceBusSharedAccessKey!
                    );

            // No connection string available - will fail validation
            return null;
        }
    }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("filter")]
    public FilterSetting? Filter { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    ///     Gets or sets the delivery schema for events sent to this subscriber.
    ///     If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; init; }

    /// <summary>
    ///     Gets or sets the retry policy for this subscriber.
    ///     If null, default Azure Event Grid retry behavior is used (enabled with 30 attempts, 24h TTL).
    /// </summary>
    [JsonPropertyName("retryPolicy")]
    public RetryPolicySettings? RetryPolicy { get; init; }

    /// <summary>
    ///     Gets or sets the dead-letter settings for this subscriber.
    ///     Events that cannot be delivered are written to the dead-letter destination.
    /// </summary>
    [JsonPropertyName("deadLetter")]
    public DeadLetterSettings? DeadLetter { get; init; }

    [JsonIgnore]
    public string SubscriberType => "serviceBus";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Subscriber name is required.", nameof(Name));

        // Validate authentication (considering topic-level defaults)
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasTopicConnectionString =
            ParentTopic != null
            && !string.IsNullOrWhiteSpace(ParentTopic.ServiceBusConnectionString);

        // Check if subscriber specifies both connection string and any namespace components
        if (hasConnectionString && HasAnySubscriberNamespaceCredential())
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' should specify either connectionString or namespace credentials, not both."
            );

        // Check if at least one authentication method is available (subscriber or topic level)
        if (
            !hasConnectionString
            && !HasSubscriberNamespaceCredentials()
            && !hasTopicConnectionString
            && !HasTopicNamespaceCredentials()
        )
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey, either at subscriber or topic level."
            );

        // Validate destination
        var hasTopic = !string.IsNullOrWhiteSpace(Topic);
        var hasQueue = !string.IsNullOrWhiteSpace(Queue);

        if (!hasTopic && !hasQueue)
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue."
            );

        if (hasTopic && hasQueue)
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue, not both."
            );

        // Validate properties
        if (Properties != null)
            foreach (var (propertyName, propertySetting) in Properties)
                propertySetting.Validate(propertyName);

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
            && !string.IsNullOrWhiteSpace(ParentTopic.ServiceBusNamespace)
            && !string.IsNullOrWhiteSpace(ParentTopic.ServiceBusSharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(ParentTopic.ServiceBusSharedAccessKey);
    }

    private static string BuildConnectionString(
        string serviceBusNamespace,
        string sharedAccessKeyName,
        string sharedAccessKey
    )
    {
        return $"Endpoint=sb://{serviceBusNamespace}.servicebus.windows.net/;SharedAccessKeyName={sharedAccessKeyName};SharedAccessKey={sharedAccessKey}";
    }
}
