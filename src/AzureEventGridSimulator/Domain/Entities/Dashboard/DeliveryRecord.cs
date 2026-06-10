using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
///     Represents the delivery status for a single subscriber endpoint.
/// </summary>
public class DeliveryRecord
{
    /// <summary>
    ///     Name of the subscriber.
    /// </summary>
    public required string SubscriberName { get; init; }

    /// <summary>
    ///     Type of subscriber (http, serviceBus, storageQueue, eventHub).
    /// </summary>
    public required string SubscriberType { get; init; }

    /// <summary>
    ///     Target endpoint (URL, queue name, etc.).
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    ///     Current delivery status.
    /// </summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>
    ///     Individual delivery attempts.
    ///     Updates are copy-on-write (see EventHistoryService): a published record's list is
    ///     never mutated, so concurrent dashboard readers can enumerate it safely.
    /// </summary>
    public List<AttemptRecord> Attempts { get; init; } = [];

    /// <summary>
    ///     When the last attempt was made.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    ///     When delivery completed (success or dead-letter).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    ///     Creates a DeliveryRecord from subscriber settings.
    /// </summary>
    public static DeliveryRecord FromSubscriber(ISubscriberSettings subscriber)
    {
        var endpoint = subscriber switch
        {
            HttpSubscriberSettings http => http.Endpoint,
            ServiceBusSubscriberSettings sb => sb.DestinationName,
            StorageQueueSubscriberSettings sq => sq.QueueName,
            EventHubSubscriberSettings eh => eh.EventHubName,
            _ => throw new InvalidOperationException(
                $"Unknown subscriber type: {subscriber.GetType().Name}"
            ),
        };

        return new DeliveryRecord
        {
            SubscriberName = subscriber.Name,
            SubscriberType = subscriber.SubscriberType,
            Endpoint = endpoint,
            Status = DeliveryStatus.Pending,
        };
    }
}
