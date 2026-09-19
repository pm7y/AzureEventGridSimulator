using System.Collections.Concurrent;
using AzureEventGridSimulator.Domain.Entities.Dashboard;

namespace AzureEventGridSimulator.Domain.Services.Dashboard;

/// <summary>
///     Thread-safe in-memory storage for event history with fixed capacity per topic.
/// </summary>
public class EventHistoryStore
{
    /// <summary>
    ///     Maximum number of events to store per topic.
    /// </summary>
    public const int MaxCapacityPerTopic = 100;

    /// <summary>
    ///     Maximum number of rejected events to store.
    /// </summary>
    public const int MaxRejectedCapacity = 50;

    /// <summary>
    ///     Dictionary for O(1) lookup by event ID.
    /// </summary>
    private readonly ConcurrentDictionary<string, EventHistoryRecord> _records = new();

    /// <summary>
    ///     Queue for FIFO eviction of rejected events.
    /// </summary>
    private readonly ConcurrentQueue<string> _rejectionOrder = new();

    /// <summary>
    ///     Dictionary for rejected events.
    /// </summary>
    private readonly ConcurrentDictionary<string, RejectedEventRecord> _rejections = new();

    /// <summary>
    ///     Per-topic eviction bookkeeping. A topic's order queue, and the records it tracks,
    ///     only change while that topic's gate is held, so they can't drift apart under
    ///     concurrent writers or a concurrent <see cref="Clear" />.
    /// </summary>
    private readonly ConcurrentDictionary<string, TopicBucket> _topics = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    ///     Lock serialising the rejection add/evict cycle.
    /// </summary>
    private readonly Lock _rejectionLock = new();

    /// <summary>
    ///     Counter for total events received (not capped by <see cref="MaxCapacityPerTopic" />).
    /// </summary>
    private int _totalEventsReceived;

    /// <summary>
    ///     Counter for total rejections (not capped by <see cref="MaxRejectedCapacity" />).
    /// </summary>
    private int _totalRejections;

    /// <summary>
    ///     Gets the total number of events received since startup or the last <see cref="Clear" />.
    /// </summary>
    public int TotalEventsReceived => _totalEventsReceived;

    /// <summary>
    ///     Gets the total number of rejections since startup or the last <see cref="Clear" />.
    /// </summary>
    public int TotalRejections => _totalRejections;

    /// <summary>
    ///     Gets the current number of events in the store.
    /// </summary>
    public int Count => _records.Count;

    /// <summary>
    ///     Gets the current number of rejections in the store.
    /// </summary>
    public int RejectionCount => _rejections.Count;

    /// <summary>
    ///     Adds an event to the store, evicting the oldest for that topic if at capacity.
    /// </summary>
    public void Add(EventHistoryRecord record)
    {
        Interlocked.Increment(ref _totalEventsReceived);

        var topic = _topics.GetOrAdd(record.TopicName, _ => new TopicBucket());

        lock (topic.Gate)
        {
            // A replayed event with the same id replaces the stored record without taking a
            // second slot in the order queue (which would corrupt the eviction accounting).
            var isNewRecord = !_records.ContainsKey(record.Id);
            _records[record.Id] = record;

            if (!isNewRecord)
            {
                return;
            }

            topic.Order.Enqueue(record.Id);

            // Evict oldest for this topic if over capacity
            while (topic.Order.Count > MaxCapacityPerTopic)
            {
                _records.TryRemove(topic.Order.Dequeue(), out _);
            }
        }
    }

    /// <summary>
    ///     Updates delivery information for an event.
    /// </summary>
    public void UpdateDelivery(string? eventId, DeliveryRecord delivery)
    {
        if (eventId != null && _records.TryGetValue(eventId, out var record))
        {
            record.AddOrUpdateDelivery(delivery);
        }
    }

    /// <summary>
    ///     Gets an event by ID.
    /// </summary>
    public EventHistoryRecord? Get(string? eventId)
    {
        return eventId != null && _records.TryGetValue(eventId, out var record) ? record : null;
    }

    /// <summary>
    ///     Gets all events ordered by received time (newest first).
    /// </summary>
    public IReadOnlyList<EventHistoryRecord> GetAll()
    {
        return _records.Values.OrderByDescending(r => r.ReceivedAt).ToList();
    }

    /// <summary>
    ///     Gets events filtered by topic name, ordered by received time (newest first).
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
    ///     Gets dashboard statistics.
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

        return new DashboardStats(
            _totalEventsReceived,
            records.Count,
            totalDelivered,
            totalFailed,
            totalPending,
            _totalRejections,
            topicsActive,
            records.MinBy(r => r.ReceivedAt)?.ReceivedAt,
            records.MaxBy(r => r.ReceivedAt)?.ReceivedAt
        );
    }

    /// <summary>
    ///     Clears all events from the store and resets counters.
    /// </summary>
    public void Clear()
    {
        // Empty each topic under the same gate Add holds, so a concurrent Add lands either
        // wholly before its topic is emptied or wholly after it.
        foreach (var topic in _topics.Values)
        {
            lock (topic.Gate)
            {
                while (topic.Order.TryDequeue(out var id))
                {
                    _records.TryRemove(id, out _);
                }
            }
        }

        lock (_rejectionLock)
        {
            _rejections.Clear();
            while (_rejectionOrder.TryDequeue(out _))
            {
                // Clear the rejections queue
            }
        }

        Interlocked.Exchange(ref _totalEventsReceived, 0);
        Interlocked.Exchange(ref _totalRejections, 0);
    }

    /// <summary>
    ///     Adds a rejected event to the store, evicting the oldest if at capacity.
    /// </summary>
    public void AddRejection(RejectedEventRecord rejection)
    {
        Interlocked.Increment(ref _totalRejections);

        lock (_rejectionLock)
        {
            _rejections[rejection.Id] = rejection;
            _rejectionOrder.Enqueue(rejection.Id);

            // Evict oldest if over capacity
            while (
                _rejections.Count > MaxRejectedCapacity
                && _rejectionOrder.TryDequeue(out var oldestId)
            )
            {
                _rejections.TryRemove(oldestId, out _);
            }
        }
    }

    /// <summary>
    ///     Gets all rejected events ordered by rejection time (newest first).
    /// </summary>
    public IReadOnlyList<RejectedEventRecord> GetAllRejections()
    {
        return _rejections.Values.OrderByDescending(r => r.RejectedAt).ToList();
    }

    /// <summary>
    ///     A topic's eviction order and the gate that guards it.
    /// </summary>
    private sealed class TopicBucket
    {
        /// <summary>
        ///     Held for the whole add/evict cycle, and by <see cref="EventHistoryStore.Clear" />.
        /// </summary>
        public Lock Gate { get; } = new();

        /// <summary>
        ///     Ids of the topic's stored events, oldest first; its count is the topic's event
        ///     count. Only touched while <see cref="Gate" /> is held.
        /// </summary>
        public Queue<string> Order { get; } = new();
    }
}
