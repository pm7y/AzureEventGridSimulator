using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator
{
    public class TopicSettings
    {
        [JsonProperty(Required = Required.Always)]
        public string Key { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int HttpsPort { get; set; }

        [JsonProperty(Required = Required.Always)]
        public ICollection<SubscriptionSettings> Subscriptions { get; set; }
    }
}