using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
///     Delivers events to Azure Event Hubs.
/// </summary>
public class EventHubEventDeliveryService(
    ILogger<EventHubEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory,
    DeliveryPropertyResolver propertyResolver
) : IEventDeliveryService, IAsyncDisposable
{
    // Lazy values guarantee a single producer per key even when GetOrAdd factories race;
    // a lost race would otherwise leak an undisposed client holding a live AMQP connection.
    private readonly ConcurrentDictionary<string, Lazy<EventHubProducerClient>> _producers = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var producer in _producers.Values.Where(p => p.IsValueCreated))
        {
            await producer.Value.DisposeAsync();
        }

        _producers.Clear();
    }

    /// <inheritdoc />
    public async Task<DeliveryResult> DeliverAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Event Hub delivery requested for event {EventId} to subscriber '{SubscriberName}'",
            delivery.Event.Id,
            delivery.Subscriber.Name
        );

        if (delivery.Subscriber is not EventHubSubscriberSettings subscription)
        {
            return new DeliveryResult(
                false,
                DeliveryOutcome.EventHubError,
                ErrorMessage: "Invalid subscriber type for Event Hub delivery"
            );
        }

        try
        {
            if (subscription.Disabled)
            {
                logger.LogWarning(
                    "Event Hub subscription '{SubscriberName}' is disabled, skipping delivery",
                    subscription.Name
                );
                return new DeliveryResult(
                    false,
                    DeliveryOutcome.EventHubError,
                    ErrorMessage: "Subscription is disabled"
                );
            }

            var deliverySchema = delivery.DeliverySchema;
            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // Serialize the event
            var json = formatter.Serialize(delivery.Event);

            // Get or create the producer
            var producer = GetOrCreateProducer(subscription);

            // Create the event data
            var eventData = new EventData(Encoding.UTF8.GetBytes(json))
            {
                ContentType = formatter.ContentType,
                MessageId = delivery.Event.Id,
            };

            // Add delivery properties
            var properties = propertyResolver.ResolveProperties(
                subscription.Properties,
                delivery.Event
            );
            foreach (var (name, value) in properties)
            {
                eventData.Properties[name] = value;
            }

            // Add standard Event Grid headers as properties
            eventData.Properties[Constants.AegEventTypeHeader] = Constants.NotificationEventType;
            eventData.Properties[Constants.AegSubscriptionNameHeader] =
                subscription.Name.ToUpperInvariant();
            eventData.Properties[Constants.AegDeliveryCountHeader] = delivery.AttemptCount + 1;
            eventData.Properties[Constants.AegOutputEventIdHeader] = delivery.Event.Id;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                eventData.Properties[Constants.AegDataVersionHeader] =
                    delivery.Event.DataVersion ?? "";
                eventData.Properties[Constants.AegMetadataVersionHeader] = "1";
            }

            // Send the event with partition key based on event ID
            var sendOptions = new SendEventOptions { PartitionKey = delivery.Event.Id };

            await producer.SendAsync([eventData], sendOptions, cancellationToken);

            logger.LogDebug(
                "Event {EventId} sent to Event Hub '{EventHubName}' via subscription '{SubscriberName}'",
                delivery.Event.Id,
                subscription.EventHubName,
                subscription.Name
            );

            return new DeliveryResult(true, DeliveryOutcome.Success);
        }
        catch (EventHubsException ex)
        {
            logger.LogWarning(
                ex,
                "Event Hub error sending event {EventId} to '{EventHubName}'",
                delivery.Event.Id,
                subscription.EventHubName
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.EventHubError,
                ErrorMessage: ex.Message
            );
        }
        catch (OperationCanceledException)
        {
            return new DeliveryResult(
                false,
                DeliveryOutcome.Cancelled,
                ErrorMessage: "Delivery cancelled"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error sending event {EventId} to Event Hub",
                delivery.Event.Id
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.EventHubError,
                ErrorMessage: ex.Message
            );
        }
    }

    private EventHubProducerClient GetOrCreateProducer(EventHubSubscriberSettings subscription)
    {
        var key = $"{subscription.EffectiveConnectionString}:{subscription.EventHubName}";

        return _producers
            .GetOrAdd(
                key,
                _ => new Lazy<EventHubProducerClient>(() =>
                {
                    // Mask the connection string for logging (show endpoint but hide key)
                    var connectionForLogging = SecretRedactor.RedactConnectionString(
                        subscription.EffectiveConnectionString
                    );

                    logger.LogInformation(
                        "Creating Event Hub producer client for subscription '{SubscriberName}' on hub '{EventHubName}'. Connection: {Connection}",
                        subscription.Name,
                        subscription.EventHubName,
                        connectionForLogging
                    );

                    var effectiveConnectionString =
                        subscription.EffectiveConnectionString
                        ?? throw new InvalidOperationException(
                            $"No connection string for Event Hub subscription '{subscription.Name}'"
                        );
                    var eventHubName =
                        subscription.EventHubName
                        ?? throw new InvalidOperationException(
                            $"No Event Hub name for subscription '{subscription.Name}'"
                        );
                    return new EventHubProducerClient(effectiveConnectionString, eventHubName);
                })
            )
            .Value;
    }
}
