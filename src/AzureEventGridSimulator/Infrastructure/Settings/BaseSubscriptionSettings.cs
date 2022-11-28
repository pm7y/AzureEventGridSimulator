using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public abstract class BaseSubscriptionSettings
{
    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "filter", Required = Required.Default)]
    public FilterSetting Filter { get; set; }

    [JsonProperty(PropertyName = "disabled", Required = Required.Default)]
    public bool Disabled { get; set; }
}
