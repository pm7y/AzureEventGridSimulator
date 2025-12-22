namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Status of event delivery to a subscriber.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// Event queued but not yet attempted.
    /// </summary>
    Pending,

    /// <summary>
    /// Delivery attempt in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Successfully delivered.
    /// </summary>
    Delivered,

    /// <summary>
    /// Failed but scheduled for retry.
    /// </summary>
    Retrying,

    /// <summary>
    /// All retries exhausted or immediate failure.
    /// </summary>
    Failed,

    /// <summary>
    /// Moved to dead-letter storage.
    /// </summary>
    DeadLettered,
}
