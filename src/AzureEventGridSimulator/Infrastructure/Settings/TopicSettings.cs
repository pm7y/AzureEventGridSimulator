using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class TopicSettings
{
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets or sets the subscribers for this topic.
    /// Supports both legacy format (array of HTTP subscribers) and new grouped format.
    /// </summary>
    [JsonPropertyName("subscribers")]
    [JsonConverter(typeof(SubscribersSettingsConverter))]
    public SubscribersSettings Subscribers { get; init; } = new();

    /// <summary>
    /// Gets or sets the expected input schema for events published to this topic.
    /// If null, the schema is auto-detected from the request.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? InputSchema { get; init; }

    /// <summary>
    /// Gets or sets the output schema for events delivered to subscribers.
    /// If null, events are delivered in the same schema they were received in.
    /// </summary>
    [JsonPropertyName("outputSchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? OutputSchema { get; init; }

    /// <summary>
    /// Gets or sets the default Service Bus connection string for subscribers.
    /// Subscribers can override this by specifying their own connection string or namespace credentials.
    /// </summary>
    [JsonPropertyName("serviceBusConnectionString")]
    public string? ServiceBusConnectionString { get; init; }

    /// <summary>
    /// Gets or sets the default Service Bus namespace (without .servicebus.windows.net suffix).
    /// Used with ServiceBusSharedAccessKeyName and ServiceBusSharedAccessKey to build a connection string.
    /// </summary>
    [JsonPropertyName("serviceBusNamespace")]
    public string? ServiceBusNamespace { get; init; }

    /// <summary>
    /// Gets or sets the default Service Bus shared access key name.
    /// </summary>
    [JsonPropertyName("serviceBusSharedAccessKeyName")]
    public string? ServiceBusSharedAccessKeyName { get; init; }

    /// <summary>
    /// Gets or sets the default Service Bus shared access key.
    /// </summary>
    [JsonPropertyName("serviceBusSharedAccessKey")]
    public string? ServiceBusSharedAccessKey { get; init; }

    /// <summary>
    /// Gets or sets the default Storage Queue connection string for subscribers.
    /// Subscribers can override this by specifying their own connection string.
    /// </summary>
    [JsonPropertyName("storageQueueConnectionString")]
    public string? StorageQueueConnectionString { get; init; }

    /// <summary>
    /// Gets or sets the default Event Hub connection string for subscribers.
    /// Subscribers can override this by specifying their own connection string or namespace credentials.
    /// </summary>
    [JsonPropertyName("eventHubConnectionString")]
    public string? EventHubConnectionString { get; init; }

    /// <summary>
    /// Gets or sets the default Event Hub namespace (without .servicebus.windows.net suffix).
    /// Used with EventHubSharedAccessKeyName and EventHubSharedAccessKey to build a connection string.
    /// </summary>
    [JsonPropertyName("eventHubNamespace")]
    public string? EventHubNamespace { get; init; }

    /// <summary>
    /// Gets or sets the default Event Hub shared access key name.
    /// </summary>
    [JsonPropertyName("eventHubSharedAccessKeyName")]
    public string? EventHubSharedAccessKeyName { get; init; }

    /// <summary>
    /// Gets or sets the default Event Hub shared access key.
    /// </summary>
    [JsonPropertyName("eventHubSharedAccessKey")]
    public string? EventHubSharedAccessKey { get; init; }
}
