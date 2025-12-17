using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Represents an event pending delivery with retry tracking.
/// </summary>
public class PendingDelivery
{
    /// <summary>
    /// Gets the unique identifier for this pending delivery.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the event to deliver.
    /// </summary>
    public required SimulatorEvent Event { get; init; }

    /// <summary>
    /// Gets or sets the subscriber to deliver to.
    /// </summary>
    public required ISubscriberSettings Subscriber { get; init; }

    /// <summary>
    /// Gets or sets the topic that received the event.
    /// </summary>
    public required TopicSettings Topic { get; init; }

    /// <summary>
    /// Gets or sets the input schema of the event.
    /// </summary>
    public required EventSchema InputSchema { get; init; }

    /// <summary>
    /// Gets or sets the time the event was enqueued.
    /// </summary>
    public DateTime EnqueuedTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the next scheduled attempt time.
    /// </summary>
    public DateTime NextAttemptTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of delivery attempts made.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets the list of delivery attempts.
    /// </summary>
    public List<DeliveryAttempt> Attempts { get; } = [];

    /// <summary>
    /// Gets the effective retry policy for this delivery.
    /// </summary>
    private RetryPolicySettings EffectiveRetryPolicy =>
        Subscriber.RetryPolicy ?? new RetryPolicySettings();

    /// <summary>
    /// Gets whether the event has expired based on TTL.
    /// </summary>
    public bool IsExpired =>
        DateTime.UtcNow > EnqueuedTime.AddMinutes(EffectiveRetryPolicy.EventTimeToLiveInMinutes);

    /// <summary>
    /// Gets whether the maximum delivery attempts have been reached.
    /// </summary>
    public bool HasReachedMaxAttempts => AttemptCount >= EffectiveRetryPolicy.MaxDeliveryAttempts;

    /// <summary>
    /// Gets whether retry is enabled for this delivery.
    /// </summary>
    public bool RetryEnabled => EffectiveRetryPolicy.Enabled;

    /// <summary>
    /// Gets the last delivery attempt, if any.
    /// </summary>
    public DeliveryAttempt LastAttempt => Attempts.Count > 0 ? Attempts[^1] : null;
}
