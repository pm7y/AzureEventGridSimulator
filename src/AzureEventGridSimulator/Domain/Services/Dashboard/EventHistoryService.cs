using System.Diagnostics.CodeAnalysis;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
///     Service for managing event history for the dashboard.
/// </summary>
public class EventHistoryService(
    EventHistoryStore store,
    SimulatorSettings settings,
    TimeProvider timeProvider,
    ILogger<EventHistoryService> logger
) : IEventHistoryService
{
    /// <inheritdoc />
    public void RecordEventReceived(SimulatorEvent evt, TopicSettings topic, EventSchema schema)
    {
        var record = EventHistoryRecord.FromSimulatorEvent(
            evt,
            topic.Name,
            topic.Port,
            schema,
            timeProvider.GetUtcNow()
        );
        store.Add(record);

        logger.LogDebug(
            "Recorded event {EventId} from topic '{TopicName}' for dashboard",
            evt.Id,
            topic.Name
        );
    }

    /// <inheritdoc />
    public void RecordDeliveryQueued(string? eventId, ISubscriberSettings subscriber)
    {
        var delivery = DeliveryRecord.FromSubscriber(subscriber);
        store.UpdateDelivery(eventId, delivery);

        logger.LogDebug(
            "Recorded delivery queued for event {EventId} to subscriber '{SubscriberName}'",
            eventId,
            subscriber.Name
        );
    }

    /// <inheritdoc />
    public void RecordDeliveryAttempt(
        string? eventId,
        string? subscriberName,
        DeliveryAttempt attempt
    )
    {
        if (!TryGetDelivery(eventId, subscriberName, "delivery attempt update", out var delivery))
        {
            return;
        }

        // Copy-on-write: dashboard readers may be enumerating the published record, so build a
        // replacement and swap it in under the record's lock via UpdateDelivery.
        var updatedDelivery = delivery with
        {
            Status = attempt.Outcome switch
            {
                DeliveryOutcome.Success => DeliveryStatus.Delivered,
                _ => DeliveryStatus.Retrying,
            },
            Attempts = [.. delivery.Attempts, AttemptRecord.FromDeliveryAttempt(attempt)],
            LastAttemptAt = attempt.AttemptTime,
        };

        store.UpdateDelivery(eventId, updatedDelivery);

        logger.LogDebug(
            "Recorded delivery attempt {AttemptNumber} for event {EventId} to '{SubscriberName}' with outcome {Outcome}",
            attempt.AttemptNumber,
            eventId,
            subscriberName,
            attempt.Outcome
        );
    }

    /// <inheritdoc />
    public void RecordDeliveryCompleted(
        string? eventId,
        string? subscriberName,
        DeliveryStatus status,
        DateTimeOffset completedAt
    )
    {
        if (
            !TryGetDelivery(eventId, subscriberName, "delivery completion update", out var delivery)
        )
        {
            return;
        }

        // Copy-on-write: see RecordDeliveryAttempt
        store.UpdateDelivery(eventId, delivery with { Status = status, CompletedAt = completedAt });

        logger.LogDebug(
            "Recorded delivery completed for event {EventId} to '{SubscriberName}' with status {Status}",
            eventId,
            subscriberName,
            status
        );
    }

    /// <inheritdoc />
    public IReadOnlyList<EventHistoryRecord> GetRecentEvents(string? topicFilter = null)
    {
        return string.IsNullOrWhiteSpace(topicFilter)
            ? store.GetAll()
            : store.GetByTopic(topicFilter);
    }

    /// <inheritdoc />
    public EventHistoryRecord? GetEvent(string eventId)
    {
        return store.Get(eventId);
    }

    /// <inheritdoc />
    public DashboardStats GetStats()
    {
        var activeTopics = settings.Topics?.Count(t => !t.Disabled) ?? 0;
        return store.GetStats(activeTopics);
    }

    /// <inheritdoc />
    public void RecordEventRejected(RejectedEventRecord rejection)
    {
        store.AddRejection(rejection);

        logger.LogDebug(
            "Recorded rejected event on topic '{TopicName}': {ErrorMessage}",
            rejection.TopicName,
            rejection.ErrorMessage
        );
    }

    /// <inheritdoc />
    public IReadOnlyList<RejectedEventRecord> GetRecentRejections()
    {
        return store.GetAllRejections();
    }

    /// <summary>
    ///     Finds the published delivery record for a subscriber of an event in the history,
    ///     logging at debug level when the event or the subscriber's delivery isn't there.
    /// </summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="subscriberName">The subscriber name.</param>
    /// <param name="operation">What the lookup is for, used in the log message.</param>
    /// <param name="delivery">The delivery record, when found.</param>
    private bool TryGetDelivery(
        string? eventId,
        string? subscriberName,
        string operation,
        [NotNullWhen(true)] out DeliveryRecord? delivery
    )
    {
        var record = store.Get(eventId);
        if (record == null)
        {
            logger.LogDebug(
                "Event {EventId} not found in history for {Operation}",
                eventId,
                operation
            );
            delivery = null;
            return false;
        }

        delivery = record.GetDeliveries().FirstOrDefault(d => d.SubscriberName == subscriberName);
        if (delivery == null)
        {
            logger.LogDebug(
                "Delivery for subscriber '{SubscriberName}' not found for event {EventId}",
                subscriberName,
                eventId
            );
            return false;
        }

        return true;
    }
}
