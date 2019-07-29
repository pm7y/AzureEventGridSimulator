using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class TopicSettings
    {
        private ICollection<SubscriptionSettings> _subscribers;

        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public int Port { get; set; }

        [JsonProperty(PropertyName = "subscribers", Required = Required.Always)]
        public ICollection<SubscriptionSettings> Subscribers { 
            get => _subscribers ?? Array.Empty<SubscriptionSettings>();
            set => _subscribers = value;
        }
    }
}
