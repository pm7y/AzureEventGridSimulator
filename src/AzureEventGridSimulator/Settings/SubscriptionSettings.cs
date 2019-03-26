using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class SubscriptionSettings
    {
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "endpoint", Required = Required.Always)]
        public string Endpoint { get; set; }

        [JsonProperty(PropertyName = "eventTypes", Required = Required.AllowNull)]
        public string[] EventTypes { get; set; }
    }
}
