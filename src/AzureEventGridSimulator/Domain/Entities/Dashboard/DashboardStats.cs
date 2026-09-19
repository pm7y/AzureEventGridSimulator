namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
///     Summary statistics for the dashboard header.
/// </summary>
/// <remarks>
///     Only <paramref name="TotalEventsReceived" /> and <paramref name="TotalRejected" /> are running
///     totals. The delivery counts are worked out from the events still in history, so they drop as
///     old events are evicted. The <c>Total*</c> names are kept because they are the property names
///     in the <c>/dashboard/api/stats</c> response.
/// </remarks>
/// <param name="TotalEventsReceived">
///     Total events received since startup or the last clear (not capped by the history limit).
/// </param>
/// <param name="EventsInHistory">
///     Current number of events in history (max 100 per topic).
/// </param>
/// <param name="TotalDelivered">
///     Count of subscriber deliveries, among the events currently in history, with status Delivered.
/// </param>
/// <param name="TotalFailed">
///     Count of subscriber deliveries, among the events currently in history, with status Failed or
///     DeadLettered.
/// </param>
/// <param name="TotalPending">
///     Count of subscriber deliveries, among the events currently in history, with status Pending,
///     InProgress or Retrying.
/// </param>
/// <param name="TotalRejected">
///     Total requests rejected by validation or parsing since startup or the last clear (not capped
///     by the number of rejections kept).
/// </param>
/// <param name="TopicsActive">
///     Number of enabled topics.
/// </param>
/// <param name="OldestEventTime">
///     Timestamp of oldest event in history.
/// </param>
/// <param name="NewestEventTime">
///     Timestamp of newest event in history.
/// </param>
public record DashboardStats(
    int TotalEventsReceived,
    int EventsInHistory,
    int TotalDelivered,
    int TotalFailed,
    int TotalPending,
    int TotalRejected,
    int TopicsActive,
    DateTimeOffset? OldestEventTime,
    DateTimeOffset? NewestEventTime
);
