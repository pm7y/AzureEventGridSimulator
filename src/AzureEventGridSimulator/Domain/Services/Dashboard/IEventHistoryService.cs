#nullable enable

using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
/// Service for managing event history for the dashboard.
/// </summary>
public interface IEventHistoryService
{
    /// <summary>
    /// Records that an event was received.
    /// </summary>
    void RecordEventReceived(SimulatorEvent evt, TopicSettings topic, EventSchema schema);

    /// <summary>
    /// Records that a delivery was queued for a subscriber.
    /// </summary>
    void RecordDeliveryQueued(string eventId, ISubscriberSettings subscriber);

    /// <summary>
    /// Records a delivery attempt.
    /// </summary>
    void RecordDeliveryAttempt(string eventId, string subscriberName, DeliveryAttempt attempt);

    /// <summary>
    /// Records that delivery completed (success or failure).
    /// </summary>
    void RecordDeliveryCompleted(
        string eventId,
        string subscriberName,
        DeliveryStatus status,
        DateTimeOffset completedAt
    );

    /// <summary>
    /// Gets recent events, optionally filtered by topic.
    /// </summary>
    IReadOnlyList<EventHistoryRecord> GetRecentEvents(string? topicFilter = null);

    /// <summary>
    /// Gets a specific event by ID.
    /// </summary>
    EventHistoryRecord? GetEvent(string eventId);

    /// <summary>
    /// Gets dashboard statistics.
    /// </summary>
    DashboardStats GetStats();

    /// <summary>
    /// Records that an event was rejected (validation failure, parse error, etc.).
    /// </summary>
    void RecordEventRejected(RejectedEventRecord rejection);

    /// <summary>
    /// Gets recent rejected events.
    /// </summary>
    IReadOnlyList<RejectedEventRecord> GetRecentRejections();
}
