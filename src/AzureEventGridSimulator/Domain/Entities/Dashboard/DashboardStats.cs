namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Summary statistics for the dashboard header.
/// </summary>
/// <param name="TotalEventsReceived" >
/// Total events received since startup (may exceed 100).
/// </param>
/// <param name="EventsInHistory" >
/// Current number of events in history (max 100).
/// </param>
/// <param name="TotalDelivered" >
/// Count of successful deliveries.
/// </param>
/// <param name="TotalFailed" >
/// Count of failed/dead-lettered deliveries.
/// </param>
/// <param name="TotalPending" >
/// Count of pending deliveries.
/// </param>
/// <param name="TotalRejected" >
/// Count of rejected events (validation/parse failures).
/// </param>
/// <param name="TopicsActive" >
/// Number of enabled topics.
/// </param>
/// <param name="OldestEventTime" >
/// Timestamp of oldest event in history.
/// </param>
/// <param name="NewestEventTime" >
/// Timestamp of newest event in history.
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
