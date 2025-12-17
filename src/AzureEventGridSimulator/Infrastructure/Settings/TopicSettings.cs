using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class TopicSettings
{
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the subscribers for this topic.
    /// Supports both legacy format (array of HTTP subscribers) and new grouped format.
    /// </summary>
    [JsonPropertyName("subscribers")]
    [JsonConverter(typeof(SubscribersSettingsConverter))]
    public SubscribersSettings Subscribers { get; set; } = new SubscribersSettings();

    /// <summary>
    /// Gets or sets the expected input schema for events published to this topic.
    /// If null, the schema is auto-detected from the request.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? InputSchema { get; set; }

    /// <summary>
    /// Gets or sets the output schema for events delivered to subscribers.
    /// If null, events are delivered in the same schema they were received in.
    /// </summary>
    [JsonPropertyName("outputSchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? OutputSchema { get; set; }
}
