using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Domain.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for Azure Service Bus subscribers (queues and topics).
/// </summary>
public class ServiceBusSubscriberSettings : ISubscriberSettings
{
    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Service Bus connection string.
    /// Either this OR (Namespace + SharedAccessKeyName + SharedAccessKey) must be provided.
    /// </summary>
    [JsonProperty(PropertyName = "connectionString", Required = Required.Default)]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Service Bus namespace (without .servicebus.windows.net suffix).
    /// </summary>
    [JsonProperty(PropertyName = "namespace", Required = Required.Default)]
    public string Namespace { get; set; }

    /// <summary>
    /// Gets or sets the shared access key name.
    /// </summary>
    [JsonProperty(PropertyName = "sharedAccessKeyName", Required = Required.Default)]
    public string SharedAccessKeyName { get; set; }

    /// <summary>
    /// Gets or sets the shared access key.
    /// </summary>
    [JsonProperty(PropertyName = "sharedAccessKey", Required = Required.Default)]
    public string SharedAccessKey { get; set; }

    /// <summary>
    /// Gets or sets the topic name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonProperty(PropertyName = "topic", Required = Required.Default)]
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the queue name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonProperty(PropertyName = "queue", Required = Required.Default)]
    public string Queue { get; set; }

    [JsonProperty(PropertyName = "filter", Required = Required.Default)]
    public FilterSetting Filter { get; set; }

    [JsonProperty(PropertyName = "disabled", Required = Required.Default)]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonProperty(PropertyName = "deliverySchema", Required = Required.Default)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EventSchema? DeliverySchema { get; set; }

    /// <summary>
    /// Gets or sets the delivery properties to add to Service Bus messages.
    /// Keys are property names, values specify whether the property is static or dynamic.
    /// </summary>
    [JsonProperty(PropertyName = "properties", Required = Required.Default)]
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
