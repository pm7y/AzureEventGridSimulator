using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
/// Service for managing event history for the dashboard.
/// </summary>
public class EventHistoryService(
    EventHistoryStore store,
    SimulatorSettings settings,
    ILogger<EventHistoryService> logger
) : IEventHistoryService
{
    /// <inheritdoc />
    public void RecordEventReceived(SimulatorEvent evt, TopicSettings topic, EventSchema schema)
    {
        var record = EventHistoryRecord.FromSimulatorEvent(evt, topic.Name, topic.Port, schema);
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
        var record = store.Get(eventId);
        if (record == null)
        {
            logger.LogDebug(
                "Event {EventId} not found in history for delivery attempt update",
                eventId
            );
            return;
        }

        var deliveries = record.GetDeliveries();
        var delivery = deliveries.FirstOrDefault(d => d.SubscriberName == subscriberName);

        if (delivery == null)
        {
            logger.LogDebug(
                "Delivery for subscriber '{SubscriberName}' not found for event {EventId}",
                subscriberName,
                eventId
            );
            return;
        }

        var attemptRecord = AttemptRecord.FromDeliveryAttempt(attempt);
        delivery.Attempts.Add(attemptRecord);
        delivery.LastAttemptAt = attempt.AttemptTime;

        // Update status based on outcome
        delivery.Status = attempt.Outcome switch
        {
            DeliveryOutcome.Success => DeliveryStatus.Delivered,
            _ => DeliveryStatus.Retrying,
        };

        store.UpdateDelivery(eventId, delivery);

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
        var record = store.Get(eventId);
        if (record == null)
        {
            logger.LogDebug(
                "Event {EventId} not found in history for delivery completion update",
                eventId
            );
            return;
        }

        var deliveries = record.GetDeliveries();
        var delivery = deliveries.FirstOrDefault(d => d.SubscriberName == subscriberName);

        if (delivery == null)
        {
            logger.LogDebug(
                "Delivery for subscriber '{SubscriberName}' not found for event {EventId}",
                subscriberName,
                eventId
            );
            return;
        }

        delivery.Status = status;
        delivery.CompletedAt = completedAt;

        store.UpdateDelivery(eventId, delivery);

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
}
