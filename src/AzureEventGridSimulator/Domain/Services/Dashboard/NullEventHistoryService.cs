using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
///     The event history service used when the dashboard is disabled. Nothing reads the history
///     then, so it records nothing and every query comes back empty.
/// </summary>
public sealed class NullEventHistoryService : IEventHistoryService
{
    private static readonly DashboardStats EmptyStats = new(0, 0, 0, 0, 0, 0, 0, null, null);

    /// <inheritdoc />
    public void RecordEventReceived(SimulatorEvent evt, TopicSettings topic, EventSchema schema) { }

    /// <inheritdoc />
    public void RecordDeliveryQueued(string? eventId, ISubscriberSettings subscriber) { }

    /// <inheritdoc />
    public void RecordDeliveryAttempt(
        string? eventId,
        string? subscriberName,
        DeliveryAttempt attempt
    ) { }

    /// <inheritdoc />
    public void RecordDeliveryCompleted(
        string? eventId,
        string? subscriberName,
        DeliveryStatus status,
        DateTimeOffset completedAt
    ) { }

    /// <inheritdoc />
    public IReadOnlyList<EventHistoryRecord> GetRecentEvents(string? topicFilter = null)
    {
        return [];
    }

    /// <inheritdoc />
    public EventHistoryRecord? GetEvent(string eventId)
    {
        return null;
    }

    /// <inheritdoc />
    public DashboardStats GetStats()
    {
        return EmptyStats;
    }

    /// <inheritdoc />
    public void RecordEventRejected(RejectedEventRecord rejection) { }

    /// <inheritdoc />
    public IReadOnlyList<RejectedEventRecord> GetRecentRejections()
    {
        return [];
    }

    /// <inheritdoc />
    public void Clear() { }
}
