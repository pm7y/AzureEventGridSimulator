using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Delivers events to Azure Event Hubs.
/// </summary>
public class EventHubEventDeliveryService(
    ILogger<EventHubEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory,
    DeliveryPropertyResolver propertyResolver
) : IEventDeliveryService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, EventHubProducerClient> _producers = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var producer in _producers.Values)
        {
            await producer.DisposeAsync();
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

            // Determine the delivery schema
            var deliverySchema =
                subscription.DeliverySchema ?? delivery.Topic.OutputSchema ?? delivery.InputSchema;
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
            eventData.Properties["aeg-event-type"] = "Notification";
            eventData.Properties["aeg-subscription-name"] = subscription.Name.ToUpperInvariant();
            eventData.Properties["aeg-delivery-count"] = delivery.AttemptCount + 1;
            eventData.Properties["aeg-output-event-id"] = delivery.Event.Id;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                eventData.Properties["aeg-data-version"] = delivery.Event.DataVersion ?? "";
                eventData.Properties["aeg-metadata-version"] = "1";
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

    /// <summary>
    /// Sends an event to an Event Hub subscriber.
    /// </summary>
    public async Task SendAsync(
        EventHubSubscriberSettings subscription,
        SimulatorEvent evt,
        TopicSettings topic,
        EventSchema inputSchema
    )
    {
        try
        {
            if (subscription.Disabled)
            {
                logger.LogWarning(
                    "Event Hub subscription '{SubscriberName}' on topic '{TopicName}' is disabled",
                    subscription.Name,
                    topic.Name
                );
                return;
            }

            // Determine the delivery schema
            var deliverySchema = subscription.DeliverySchema ?? topic.OutputSchema ?? inputSchema;
            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // Serialize the event
            var json = formatter.Serialize(evt);

            // Get or create the producer
            var producer = GetOrCreateProducer(subscription);

            // Create the event data
            var eventData = new EventData(Encoding.UTF8.GetBytes(json))
            {
                ContentType = formatter.ContentType,
                MessageId = evt.Id,
            };

            // Add delivery properties
            var properties = propertyResolver.ResolveProperties(subscription.Properties, evt);
            foreach (var (name, value) in properties)
            {
                eventData.Properties[name] = value;
            }

            // Add standard Event Grid headers as properties
            eventData.Properties["aeg-event-type"] = "Notification";
            eventData.Properties["aeg-subscription-name"] = subscription.Name.ToUpperInvariant();
            eventData.Properties["aeg-delivery-count"] = 1;
            eventData.Properties["aeg-output-event-id"] = evt.Id;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                eventData.Properties["aeg-data-version"] = evt.DataVersion ?? "";
                eventData.Properties["aeg-metadata-version"] = "1";
            }

            // Send the event with partition key based on event ID
            var sendOptions = new SendEventOptions { PartitionKey = evt.Id };

            await producer.SendAsync([eventData], sendOptions);

            logger.LogDebug(
                "Event {EventId} sent to Event Hub '{EventHubName}' via subscription '{SubscriberName}' on topic '{TopicName}'",
                evt.Id,
                subscription.EventHubName,
                subscription.Name,
                topic.Name
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send event {EventId} to Event Hub '{EventHubName}' via subscription '{SubscriberName}'",
                evt.Id,
                subscription.EventHubName,
                subscription.Name
            );
        }
    }

    private EventHubProducerClient GetOrCreateProducer(EventHubSubscriberSettings subscription)
    {
        var key = $"{subscription.EffectiveConnectionString}:{subscription.EventHubName}";

        return _producers.GetOrAdd(
            key,
            _ =>
            {
                // Mask the connection string for logging (show endpoint but hide key)
                var connectionForLogging = subscription.EffectiveConnectionString;
                var keyIndex = connectionForLogging?.IndexOf(
                    "SharedAccessKey=",
                    StringComparison.OrdinalIgnoreCase
                );
                if (keyIndex is > 0 && connectionForLogging != null)
                {
                    connectionForLogging =
                        connectionForLogging[..(keyIndex.Value + 16)] + "***REDACTED***";
                }

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
            }
        );
    }
}
