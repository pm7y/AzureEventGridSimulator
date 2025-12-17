using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for Azure Service Bus subscribers (queues and topics).
/// </summary>
public class ServiceBusSubscriberSettings : ISubscriberSettings
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Service Bus connection string.
    /// Either this OR (Namespace + SharedAccessKeyName + SharedAccessKey) must be provided.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Service Bus namespace (without .servicebus.windows.net suffix).
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
    /// Gets or sets the topic name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the queue name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("queue")]
    public string Queue { get; set; }

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
    /// Gets or sets the delivery properties to add to Service Bus messages.
    /// Keys are property names, values specify whether the property is static or dynamic.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, DeliveryPropertySettings> Properties { get; set; }

    [JsonIgnore]
    public string SubscriberType => "serviceBus";

    /// <summary>
    /// Gets the destination name (either topic or queue).
    /// </summary>
    [JsonIgnore]
    public string DestinationName => !string.IsNullOrWhiteSpace(Topic) ? Topic : Queue;

    /// <summary>
    /// Gets whether the destination is a topic (vs a queue).
    /// </summary>
    [JsonIgnore]
    public bool IsTopic => !string.IsNullOrWhiteSpace(Topic);

    /// <summary>
    /// Gets the connection string, either directly specified or built from components.
    /// </summary>
    [JsonIgnore]
    public string EffectiveConnectionString
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString;
            }

            return $"Endpoint=sb://{Namespace}.servicebus.windows.net/;SharedAccessKeyName={SharedAccessKeyName};SharedAccessKey={SharedAccessKey}";
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }

        // Validate authentication
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasNamespaceCredentials =
            !string.IsNullOrWhiteSpace(Namespace)
            && !string.IsNullOrWhiteSpace(SharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(SharedAccessKey);

        if (!hasConnectionString && !hasNamespaceCredentials)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey."
            );
        }

        if (hasConnectionString && hasNamespaceCredentials)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' should specify either connectionString or namespace credentials, not both."
            );
        }

        // Validate destination
        var hasTopic = !string.IsNullOrWhiteSpace(Topic);
        var hasQueue = !string.IsNullOrWhiteSpace(Queue);

        if (!hasTopic && !hasQueue)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue."
            );
        }

        if (hasTopic && hasQueue)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue, not both."
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
    }
}
