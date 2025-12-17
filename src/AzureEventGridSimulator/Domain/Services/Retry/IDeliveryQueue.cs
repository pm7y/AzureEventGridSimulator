using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
/// Interface for the in-memory queue of pending event deliveries.
/// </summary>
public interface IDeliveryQueue
{
    /// <summary>
    /// Gets the current count of pending deliveries.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Enqueues a pending delivery for processing.
    /// </summary>
    /// <param name="delivery" >
    /// The pending delivery to enqueue.
    /// </param>
    void Enqueue(PendingDelivery delivery);

    /// <summary>
    /// Removes a delivery from the queue.
    /// </summary>
    /// <param name="deliveryId" >
    /// The ID of the delivery to remove.
    /// </param>
    /// <returns>
    /// True if the delivery was removed.
    /// </returns>
    bool Remove(string deliveryId);

    /// <summary>
    /// Requeues a delivery for retry with updated next attempt time.
    /// </summary>
    /// <param name="delivery" >
    /// The delivery to requeue.
    /// </param>
    void RequeueForRetry(PendingDelivery delivery);

    /// <summary>
    /// Gets all deliveries that are due for processing.
    /// </summary>
    /// <returns>
    /// Enumerable of due deliveries.
    /// </returns>
    IEnumerable<PendingDelivery> GetDueDeliveries();
}
