using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static AzureEventGridSimulator.Domain.Commands.SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<AzureEventGridSimulator.Domain.Entities.EventGridEvent>;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class AzureServiceBusSubscriptionSettings : BaseSubscriptionSettings
{
    [JsonProperty(PropertyName = "namespace", Required = Required.Always)]
    public string Namespace { get; set; }

    [JsonProperty(PropertyName = "properties", Required = Required.Default)]
    public Dictionary<string, Property> Properties { get; set; } = new Dictionary<string, Property>();

    [JsonProperty(PropertyName = "sharedAccessKeyName", Required = Required.Always)]
    public string SharedAccessKeyName { get; set; }

    [JsonProperty(PropertyName = "sharedAccessKey", Required = Required.Always)]
    public string SharedAccessKey { get; set; }

    [JsonProperty(PropertyName = "topic", Required = Required.Always)]
    public string Topic { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Service bus 'name' is required");
        }

        if (string.IsNullOrWhiteSpace(Namespace) || !Regex.IsMatch(Namespace, "^[a-z][a-z0-9-]{5,49}(?<!-|-sb|-mgt)$"))
        {
            // https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal
            throw new InvalidOperationException($"{Name}: 'namespace' is invalid. The length must be at least 6 and at most 50 characters. It can contain only letters, numbers, hyphens \"-\" and must start with a letter and end with a letter or number. It cannot end with \"-sb\" or \"-mgmt\".");
        }

        if (string.IsNullOrWhiteSpace(SharedAccessKey))
        {
            throw new InvalidOperationException("'sharedAccessKey' is required");
        }

        if (string.IsNullOrWhiteSpace(SharedAccessKeyName))
        {
            throw new InvalidOperationException("'sharedAccessKeyName' is required");
        }

        if (string.IsNullOrWhiteSpace(Topic) || !Regex.IsMatch(Topic, "^[a-z][a-z0-9-]{2,62}(?<!-)$"))
        {
            // https://learn.microsoft.com/en-us/rest/api/storageservices/naming-queues-and-metadata
            throw new InvalidOperationException($"{Name}: 'topic' is invalid. The length must be at least 3 and at most 63 characters. It can contain only letters, numbers, hyphens \"-\" and must start with a letter and end with a letter or number.");
        }

        if (Properties.TryGetValue(BrokerPropertyKeys.MessageId, out var property) && property.PropertyType == PropertyType.Static)
        {
            // https://learn.microsoft.com/en-us/azure/event-grid/delivery-properties
            throw new InvalidOperationException($"{Name}: {BrokerPropertyKeys.MessageId} only supports dynamic values.");
        }

        if (Properties.ContainsKey(BrokerPropertyKeys.MessageId) && Properties.ContainsKey(BrokerPropertyKeys.SessionId))
        {
            // https://learn.microsoft.com/en-us/azure/event-grid/delivery-properties
            throw new InvalidOperationException($"{Name}: Either {BrokerPropertyKeys.MessageId} or {BrokerPropertyKeys.SessionId} can be set, but not both.");
        }
    }

    public class Property
    {
        public Property()
        {
        }

        public Property(PropertyType propertyType, string value)
        {
            PropertyType = propertyType;
            Value = value;
        }

        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyType PropertyType { get; set; }

        [JsonProperty(PropertyName = "value", Required = Required.Always)]
        public string Value { get; set; }
    }

    public enum PropertyType
    {
        [EnumMember(Value = "dynamic")]
        Dynamic,

        [EnumMember(Value = "static")]
        Static
    }
}
