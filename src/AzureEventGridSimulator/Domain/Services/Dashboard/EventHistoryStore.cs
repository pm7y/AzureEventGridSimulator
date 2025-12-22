#nullable enable

using System.Collections.Concurrent;
using AzureEventGridSimulator.Domain.Entities.Dashboard;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
/// Thread-safe in-memory storage for event history with fixed capacity.
/// </summary>
public class EventHistoryStore
{
    /// <summary>
    /// Maximum number of events to store.
    /// </summary>
    public const int MaxCapacity = 100;

    /// <summary>
    /// Maximum number of rejected events to store.
    /// </summary>
    public const int MaxRejectedCapacity = 50;

    /// <summary>
    /// Queue for FIFO eviction tracking.
    /// </summary>
    private readonly ConcurrentQueue<string> _order = new();

    /// <summary>
    /// Dictionary for O(1) lookup by event ID.
    /// </summary>
    private readonly ConcurrentDictionary<string, EventHistoryRecord> _records = new();

    /// <summary>
    /// Queue for FIFO eviction of rejected events.
    /// </summary>
    private readonly ConcurrentQueue<string> _rejectionOrder = new();

    /// <summary>
    /// Dictionary for rejected events.
    /// </summary>
    private readonly ConcurrentDictionary<string, RejectedEventRecord> _rejections = new();

    /// <summary>
    /// Counter for total events received (may exceed MaxCapacity).
    /// </summary>
    private int _totalEventsReceived;

    /// <summary>
    /// Counter for total rejections.
    /// </summary>
    private int _totalRejections;

    /// <summary>
    /// Gets the total number of events received since startup.
    /// </summary>
    public int TotalEventsReceived => _totalEventsReceived;

    /// <summary>
    /// Gets the total number of rejections since startup.
    /// </summary>
    public int TotalRejections => _totalRejections;

    /// <summary>
    /// Gets the current number of events in the store.
    /// </summary>
    public int Count => _records.Count;

    /// <summary>
    /// Gets the current number of rejections in the store.
    /// </summary>
    public int RejectionCount => _rejections.Count;

    /// <summary>
    /// Adds an event to the store, evicting the oldest if at capacity.
    /// </summary>
    public void Add(EventHistoryRecord record)
    {
        Interlocked.Increment(ref _totalEventsReceived);

        _records[record.Id] = record;
        _order.Enqueue(record.Id);

        // Evict oldest if over capacity
        while (_records.Count > MaxCapacity && _order.TryDequeue(out var oldestId))
        {
            _records.TryRemove(oldestId, out _);
        }
    }

    /// <summary>
    /// Updates delivery information for an event.
    /// </summary>
    public void UpdateDelivery(string eventId, DeliveryRecord delivery)
    {
        if (_records.TryGetValue(eventId, out var record))
        {
            record.AddOrUpdateDelivery(delivery);
        }
    }

    /// <summary>
    /// Gets an event by ID.
    /// </summary>
    public EventHistoryRecord? Get(string eventId)
    {
        return _records.TryGetValue(eventId, out var record) ? record : null;
    }

    /// <summary>
    /// Gets all events ordered by received time (newest first).
    /// </summary>
    public IReadOnlyList<EventHistoryRecord> GetAll()
    {
        return _records.Values.OrderByDescending(r => r.ReceivedAt).ToList();
    }

    /// <summary>
    /// Gets events filtered by topic name, ordered by received time (newest first).
    /// </summary>
    public IReadOnlyList<EventHistoryRecord> GetByTopic(string topicName)
    {
        return _records
            .Values.Where(r =>
                string.Equals(r.TopicName, topicName, StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(r => r.ReceivedAt)
            .ToList();
    }

    /// <summary>
    /// Gets dashboard statistics.
    /// </summary>
    public DashboardStats GetStats(int topicsActive)
    {
        var records = _records.Values.ToList();

        var totalDelivered = 0;
        var totalFailed = 0;
        var totalPending = 0;

        foreach (var record in records)
        {
            foreach (var delivery in record.GetDeliveries())
            {
                switch (delivery.Status)
                {
                    case DeliveryStatus.Delivered:
                        totalDelivered++;
                        break;
                    case DeliveryStatus.Failed:
                    case DeliveryStatus.DeadLettered:
                        totalFailed++;
                        break;
                    case DeliveryStatus.Pending:
                    case DeliveryStatus.InProgress:
                    case DeliveryStatus.Retrying:
                        totalPending++;
                        break;
                }
            }
        }

        return new DashboardStats
        {
            TotalEventsReceived = _totalEventsReceived,
            EventsInHistory = records.Count,
            TotalDelivered = totalDelivered,
            TotalFailed = totalFailed,
            TotalPending = totalPending,
            TotalRejected = _totalRejections,
            TopicsActive = topicsActive,
            OldestEventTime = records.MinBy(r => r.ReceivedAt)?.ReceivedAt,
            NewestEventTime = records.MaxBy(r => r.ReceivedAt)?.ReceivedAt,
        };
    }

    /// <summary>
    /// Clears all events from the store and resets counters.
    /// </summary>
    public void Clear()
    {
        _records.Clear();
        while (_order.TryDequeue(out _))
        {
            // Clear the queue
        }

        _rejections.Clear();
        while (_rejectionOrder.TryDequeue(out _))
        {
            // Clear the rejections queue
        }

        Interlocked.Exchange(ref _totalEventsReceived, 0);
        Interlocked.Exchange(ref _totalRejections, 0);
    }

    /// <summary>
    /// Adds a rejected event to the store, evicting the oldest if at capacity.
    /// </summary>
    public void AddRejection(RejectedEventRecord rejection)
    {
        Interlocked.Increment(ref _totalRejections);

        _rejections[rejection.Id] = rejection;
        _rejectionOrder.Enqueue(rejection.Id);

        // Evict oldest if over capacity
        while (
            _rejections.Count > MaxRejectedCapacity && _rejectionOrder.TryDequeue(out var oldestId)
        )
        {
            _rejections.TryRemove(oldestId, out _);
        }
    }

    /// <summary>
    /// Gets all rejected events ordered by rejection time (newest first).
    /// </summary>
    public IReadOnlyList<RejectedEventRecord> GetAllRejections()
    {
        return _rejections.Values.OrderByDescending(r => r.RejectedAt).ToList();
    }
}
