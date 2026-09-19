using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     The settings every subscriber type shares. Each subclass keeps its own
///     <see cref="Validate" /> so that the order of its checks, and therefore the first error a
///     user sees, stays under that subclass's control.
/// </summary>
public abstract class SubscriberSettingsBase : ISubscriberSettings
{
    /// <summary>
    ///     Internal reference to the parent topic for connection string inheritance.
    ///     Set by <see cref="SimulatorSettings.Validate" /> before any subscriber is validated.
    /// </summary>
    [JsonIgnore]
    internal TopicSettings? ParentTopic { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("filter")]
    public FilterSetting? Filter { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    ///     Gets or sets the delivery schema for events sent to this subscriber.
    ///     If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; init; }

    /// <summary>
    ///     Gets or sets the retry policy for this subscriber.
    ///     If null, default Azure Event Grid retry behavior is used (enabled with 30 attempts, 24h TTL).
    /// </summary>
    [JsonPropertyName("retryPolicy")]
    public RetryPolicySettings? RetryPolicy { get; init; }

    /// <summary>
    ///     Gets or sets the dead-letter settings for this subscriber.
    ///     Events that cannot be delivered are written to the dead-letter destination.
    /// </summary>
    [JsonPropertyName("deadLetter")]
    public DeadLetterSettings? DeadLetter { get; init; }

    // Overrides must repeat [JsonIgnore]: System.Text.Json reads it from the overriding
    // property, not from this declaration.
    [JsonIgnore]
    public abstract string SubscriberType { get; }

    public abstract void Validate();

    /// <summary>
    ///     The first check every subscriber type makes.
    /// </summary>
    protected void ValidateName()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }
    }

    /// <summary>
    ///     The last checks every subscriber type makes: filter, then retry policy, then dead-letter.
    /// </summary>
    protected void ValidateCommonTail()
    {
        Filter?.Validate();
        RetryPolicy?.Validate();
        DeadLetter?.Validate();
    }
}
