using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class TopicSettings
    {
        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public int Port { get; set; }

        [JsonProperty(PropertyName = "subscribers", Required = Required.Always)]
        public ICollection<SubscriptionSettings> Subscribers { get; set; }
    }
}
