using System.Collections.Concurrent;
using AzureEventGridSimulator.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
/// Thread-safe in-memory implementation of the delivery queue.
/// Events are lost on restart (acceptable for a simulator).
/// </summary>
public class InMemoryDeliveryQueue(ILogger<InMemoryDeliveryQueue> logger) : IDeliveryQueue
{
    private readonly ConcurrentDictionary<string, PendingDelivery> _queue = new();

    /// <inheritdoc/>
    public void Enqueue(PendingDelivery delivery)
    {
        if (_queue.TryAdd(delivery.Id, delivery))
        {
            logger.LogDebug(
                "Enqueued delivery {DeliveryId} for event {EventId} to subscriber '{SubscriberName}'",
                delivery.Id,
                delivery.Event.Id,
                delivery.Subscriber.Name
            );
        }
        else
        {
            logger.LogWarning(
                "Failed to enqueue delivery {DeliveryId} - already exists",
                delivery.Id
            );
        }
    }

    /// <inheritdoc/>
    public bool Remove(string deliveryId)
    {
        return _queue.TryRemove(deliveryId, out _);
    }

    /// <inheritdoc/>
    public void RequeueForRetry(PendingDelivery delivery)
    {
        // Update the delivery in place or add it back
        _queue.AddOrUpdate(delivery.Id, delivery, (_, _) => delivery);

        logger.LogDebug(
            "Requeued delivery {DeliveryId} for retry at {NextAttempt}. Attempt {AttemptCount}",
            delivery.Id,
            delivery.NextAttemptTime,
            delivery.AttemptCount
        );
    }

    /// <inheritdoc/>
    public IEnumerable<PendingDelivery> GetDueDeliveries()
    {
        var now = DateTime.UtcNow;

        return _queue
            .Values.Where(d => d.NextAttemptTime <= now)
            .OrderBy(d => d.NextAttemptTime)
            .ToList(); // Materialize to avoid modification during enumeration
    }

    /// <inheritdoc/>
    public int Count => _queue.Count;
}
