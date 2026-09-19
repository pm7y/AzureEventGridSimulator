using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
///     Represents the delivery status for a single subscriber endpoint.
///     Immutable: EventHistoryService records an update by publishing a copy made with a
///     <c>with</c> expression, so concurrent dashboard readers never see a record change.
/// </summary>
public sealed record DeliveryRecord
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
    public DeliveryStatus Status { get; init; } = DeliveryStatus.Pending;

    /// <summary>
    ///     Individual delivery attempts.
    ///     A published record's list is never mutated (an update gets a new list), so
    ///     concurrent dashboard readers can enumerate it safely.
    /// </summary>
    public List<AttemptRecord> Attempts { get; init; } = [];

    /// <summary>
    ///     When the last attempt was made.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>
    ///     When delivery completed (success or dead-letter).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

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
