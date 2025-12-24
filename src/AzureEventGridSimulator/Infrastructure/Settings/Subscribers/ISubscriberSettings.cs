using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Common interface for all subscriber types (HTTP, Service Bus, etc.).
/// </summary>
public interface ISubscriberSettings
{
    /// <summary>
    /// Gets the name of the subscription.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the filter settings for this subscription.
    /// </summary>
    FilterSetting? Filter { get; }

    /// <summary>
    /// Gets whether this subscription is disabled.
    /// </summary>
    bool Disabled { get; }

    /// <summary>
    /// Gets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    EventSchema? DeliverySchema { get; }

    /// <summary>
    /// Gets the type of subscriber (http, serviceBus, etc.).
    /// </summary>
    string SubscriberType { get; }

    /// <summary>
    /// Gets the retry policy for this subscriber.
    /// If null, default Azure Event Grid retry behavior is used.
    /// </summary>
    RetryPolicySettings? RetryPolicy { get; }

    /// <summary>
    /// Gets the dead-letter settings for this subscriber.
    /// Events that cannot be delivered are written to the dead-letter destination.
    /// </summary>
    DeadLetterSettings? DeadLetter { get; }

    /// <summary>
    /// Validates the subscriber settings.
    /// </summary>
    void Validate();
}
