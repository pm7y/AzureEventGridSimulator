using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class TopicSettings
{
    [JsonProperty(PropertyName = "key", Required = Required.Always)]
    public string Key { get; set; }

    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "port", Required = Required.Always)]
    public int Port { get; set; }

    [JsonProperty(PropertyName = "disabled", Required = Required.Default)]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the subscribers for this topic.
    /// Supports both legacy format (array of HTTP subscribers) and new grouped format.
    /// </summary>
    [JsonProperty(PropertyName = "subscribers", Required = Required.Default)]
    [JsonConverter(typeof(SubscribersSettingsConverter))]
    public SubscribersSettings Subscribers { get; set; } = new SubscribersSettings();

    /// <summary>
    /// Gets or sets the expected input schema for events published to this topic.
    /// If null, the schema is auto-detected from the request.
    /// </summary>
    [JsonProperty(PropertyName = "inputSchema", Required = Required.Default)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EventSchema? InputSchema { get; set; }

    /// <summary>
    /// Gets or sets the output schema for events delivered to subscribers.
    /// If null, events are delivered in the same schema they were received in.
    /// </summary>
    [JsonProperty(PropertyName = "outputSchema", Required = Required.Default)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EventSchema? OutputSchema { get; set; }
}
