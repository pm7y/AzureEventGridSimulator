namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Summary statistics for the dashboard header.
/// </summary>
public class DashboardStats
{
    /// <summary>
    /// Total events received since startup (may exceed 100).
    /// </summary>
    public int TotalEventsReceived { get; init; }

    /// <summary>
    /// Current number of events in history (max 100).
    /// </summary>
    public int EventsInHistory { get; init; }

    /// <summary>
    /// Count of successful deliveries.
    /// </summary>
    public int TotalDelivered { get; init; }

    /// <summary>
    /// Count of failed/dead-lettered deliveries.
    /// </summary>
    public int TotalFailed { get; init; }

    /// <summary>
    /// Count of pending deliveries.
    /// </summary>
    public int TotalPending { get; init; }

    /// <summary>
    /// Count of rejected events (validation/parse failures).
    /// </summary>
    public int TotalRejected { get; init; }

    /// <summary>
    /// Number of enabled topics.
    /// </summary>
    public int TopicsActive { get; init; }

    /// <summary>
    /// Timestamp of oldest event in history.
    /// </summary>
    public DateTimeOffset? OldestEventTime { get; init; }

    /// <summary>
    /// Timestamp of newest event in history.
    /// </summary>
    public DateTimeOffset? NewestEventTime { get; init; }
}
