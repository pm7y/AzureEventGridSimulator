#nullable enable

using System.Text.Json;

namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Represents a single event as captured for dashboard display.
/// Wraps the existing SimulatorEvent with additional metadata.
/// </summary>
public class EventHistoryRecord
{
    /// <summary>
    /// The lock object for thread-safe updates to deliveries.
    /// </summary>
    private readonly object _deliveriesLock = new();

    /// <summary>
    /// Unique identifier (from event).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// When the event was received by the simulator.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Name of the topic that received the event.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Port the topic is listening on.
    /// </summary>
    public int TopicPort { get; init; }

    /// <summary>
    /// Event type identifier.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Event subject.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Event source URI.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// When the event occurred (from payload).
    /// </summary>
    public string? EventTime { get; init; }

    /// <summary>
    /// Schema type (EventGrid, CloudEvents).
    /// </summary>
    public EventSchema InputSchema { get; init; }

    /// <summary>
    /// Full event payload as JSON string.
    /// </summary>
    public required string PayloadJson { get; init; }

    /// <summary>
    /// Delivery attempts to subscribers.
    /// </summary>
    public List<DeliveryRecord> Deliveries { get; } = [];

    /// <summary>
    /// Creates an EventHistoryRecord from a SimulatorEvent.
    /// </summary>
    public static EventHistoryRecord FromSimulatorEvent(
        SimulatorEvent evt,
        string topicName,
        int topicPort,
        EventSchema inputSchema
    )
    {
        var payloadJson = evt.Schema switch
        {
            EventSchema.EventGridSchema => JsonSerializer.Serialize(
                evt.EventGridEvent,
                new JsonSerializerOptions { WriteIndented = true }
            ),
            EventSchema.CloudEventV1_0 => JsonSerializer.Serialize(
                evt.CloudEvent,
                new JsonSerializerOptions { WriteIndented = true }
            ),
            _ => "{}",
        };

        return new EventHistoryRecord
        {
            Id = evt.Id,
            ReceivedAt = DateTimeOffset.UtcNow,
            TopicName = topicName,
            TopicPort = topicPort,
            EventType = evt.EventType ?? "Unknown",
            Subject = evt.Subject,
            Source = evt.Source,
            EventTime = evt.EventTime,
            InputSchema = inputSchema,
            PayloadJson = payloadJson,
        };
    }

    /// <summary>
    /// Thread-safe method to add or update a delivery record.
    /// </summary>
    public void AddOrUpdateDelivery(DeliveryRecord delivery)
    {
        lock (_deliveriesLock)
        {
            var existing = Deliveries.FirstOrDefault(d =>
                d.SubscriberName == delivery.SubscriberName
            );
            if (existing != null)
            {
                Deliveries.Remove(existing);
            }

            Deliveries.Add(delivery);
        }
    }

    /// <summary>
    /// Thread-safe method to get delivery records.
    /// </summary>
    public IReadOnlyList<DeliveryRecord> GetDeliveries()
    {
        lock (_deliveriesLock)
        {
            return Deliveries.ToList();
        }
    }
}
