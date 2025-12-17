using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Common interface for all subscriber types (HTTP, Service Bus, etc.).
/// </summary>
public interface ISubscriberSettings
{
    /// <summary>
    /// Gets or sets the name of the subscription.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the filter settings for this subscription.
    /// </summary>
    FilterSetting Filter { get; set; }

    /// <summary>
    /// Gets or sets whether this subscription is disabled.
    /// </summary>
    bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    EventSchema? DeliverySchema { get; set; }

    /// <summary>
    /// Gets the type of subscriber (http, serviceBus, etc.).
    /// </summary>
    string SubscriberType { get; }

    /// <summary>
    /// Validates the subscriber settings.
    /// </summary>
    void Validate();
}
