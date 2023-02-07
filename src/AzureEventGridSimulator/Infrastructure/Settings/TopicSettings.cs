using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class TopicSettings
{
    [JsonProperty(PropertyName = "key", Required = Required.Always)]
    public string Key { get; set; }

    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "port", Required = Required.Always)]
    public int Port { get; set; }

    [JsonProperty(PropertyName = "type", Required = Required.Always)]
    public EventType Type { get; set; }

    [JsonProperty(PropertyName = "disabled", Required = Required.Default)]
    public bool Disabled { get; set; }

    [JsonProperty(PropertyName = "subscribers", Required = Required.Default)]
    public SubscriberSettings Subscribers { get; set; } = new SubscriberSettings();

    internal void Validate()
    {
        Subscribers.Validate();
    }
}
