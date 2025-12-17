namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Represents the outcome of a delivery attempt.
/// </summary>
public enum DeliveryOutcome
{
    /// <summary>
    /// Delivery was successful.
    /// </summary>
    Success,

    /// <summary>
    /// HTTP endpoint returned an error status code.
    /// </summary>
    HttpError,

    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Network error (connection refused, DNS failure, etc.).
    /// </summary>
    NetworkError,

    /// <summary>
    /// Error sending to Service Bus.
    /// </summary>
    ServiceBusError,

    /// <summary>
    /// Error sending to Storage Queue.
    /// </summary>
    StorageQueueError,

    /// <summary>
    /// Request was cancelled.
    /// </summary>
    Cancelled,
}
