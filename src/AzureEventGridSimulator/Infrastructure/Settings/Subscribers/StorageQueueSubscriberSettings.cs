using System;
using AzureEventGridSimulator.Domain.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for Azure Storage Queue subscribers.
/// </summary>
public class StorageQueueSubscriberSettings : ISubscriberSettings
{
    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Storage Queue connection string.
    /// </summary>
    [JsonProperty(PropertyName = "connectionString", Required = Required.Always)]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    [JsonProperty(PropertyName = "queueName", Required = Required.Always)]
    public string QueueName { get; set; }

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

    [JsonIgnore]
    public string SubscriberType => "storageQueue";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a connectionString."
            );
        }

        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a queueName."
            );
        }

        Filter?.Validate();
    }
}
