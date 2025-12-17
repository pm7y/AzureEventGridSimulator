using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.ServiceBus;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Delivers events to Azure Service Bus queues and topics.
/// </summary>
public class ServiceBusEventDeliveryService(
    ILogger<ServiceBusEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory,
    DeliveryPropertyResolver propertyResolver
) : IEventDeliveryService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }

        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }

        _senders.Clear();
        _clients.Clear();
    }

    /// <inheritdoc />
    public async Task<DeliveryResult> DeliverAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        if (delivery.Subscriber is not ServiceBusSubscriberSettings subscription)
        {
            return new DeliveryResult(
                false,
                DeliveryOutcome.ServiceBusError,
                ErrorMessage: "Invalid subscriber type for Service Bus delivery"
            );
        }

        try
        {
            if (subscription.Disabled)
            {
                return new DeliveryResult(
                    false,
                    DeliveryOutcome.ServiceBusError,
                    ErrorMessage: "Subscription is disabled"
                );
            }

            // Determine the delivery schema
            var deliverySchema =
                subscription.DeliverySchema ?? delivery.Topic.OutputSchema ?? delivery.InputSchema;
            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // Serialize the event
            var json = formatter.Serialize(delivery.Event);

            // Get or create the sender
            var sender = GetOrCreateSender(subscription);

            // Create the message
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
            {
                ContentType = formatter.ContentType,
                MessageId = delivery.Event.Id ?? Guid.NewGuid().ToString(),
            };

            // Add delivery properties
            var properties = propertyResolver.ResolveProperties(
                subscription.Properties,
                delivery.Event
            );
            foreach (var (name, value) in properties)
            {
                message.ApplicationProperties[name] = value;
            }

            // Add standard Event Grid headers as application properties
            message.ApplicationProperties["aeg-event-type"] = "Notification";
            message.ApplicationProperties["aeg-subscription-name"] =
                subscription.Name.ToUpperInvariant();
            message.ApplicationProperties["aeg-delivery-count"] = delivery.AttemptCount;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                message.ApplicationProperties["aeg-data-version"] =
                    delivery.Event.DataVersion ?? "";
                message.ApplicationProperties["aeg-metadata-version"] = "1";
            }

            // Send the message
            await sender.SendMessageAsync(message, cancellationToken);

            logger.LogDebug(
                "Event {EventId} sent to Service Bus {DestinationType} '{DestinationName}' via subscription '{SubscriberName}'",
                delivery.Event.Id,
                subscription.IsTopic ? "topic" : "queue",
                subscription.DestinationName,
                subscription.Name
            );

            return new DeliveryResult(true, DeliveryOutcome.Success);
        }
        catch (ServiceBusException ex)
        {
            logger.LogWarning(
                ex,
                "Service Bus error sending event {EventId} to {DestinationType} '{DestinationName}'",
                delivery.Event.Id,
                subscription.IsTopic ? "topic" : "queue",
                subscription.DestinationName
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.ServiceBusError,
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
                "Unexpected error sending event {EventId} to Service Bus",
                delivery.Event.Id
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.ServiceBusError,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// Sends an event to a Service Bus subscriber.
    /// </summary>
    public async Task SendAsync(
        ServiceBusSubscriberSettings subscription,
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
                    "Service Bus subscription '{SubscriberName}' on topic '{TopicName}' is disabled",
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

            // Get or create the sender
            var sender = GetOrCreateSender(subscription);

            // Create the message
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
            {
                ContentType = formatter.ContentType,
                MessageId = evt.Id ?? Guid.NewGuid().ToString(),
            };

            // Add delivery properties
            var properties = propertyResolver.ResolveProperties(subscription.Properties, evt);
            foreach (var (name, value) in properties)
            {
                message.ApplicationProperties[name] = value;
            }

            // Add standard Event Grid headers as application properties
            message.ApplicationProperties["aeg-event-type"] = "Notification";
            message.ApplicationProperties["aeg-subscription-name"] =
                subscription.Name.ToUpperInvariant();
            message.ApplicationProperties["aeg-delivery-count"] = 0;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                message.ApplicationProperties["aeg-data-version"] = evt.DataVersion ?? "";
                message.ApplicationProperties["aeg-metadata-version"] = "1";
            }

            // Send the message
            await sender.SendMessageAsync(message);

            logger.LogDebug(
                "Event {EventId} sent to Service Bus {DestinationType} '{DestinationName}' via subscription '{SubscriberName}' on topic '{TopicName}'",
                evt.Id,
                subscription.IsTopic ? "topic" : "queue",
                subscription.DestinationName,
                subscription.Name,
                topic.Name
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send event {EventId} to Service Bus {DestinationType} '{DestinationName}' via subscription '{SubscriberName}'",
                evt.Id,
                subscription.IsTopic ? "topic" : "queue",
                subscription.DestinationName,
                subscription.Name
            );
        }
    }

    private ServiceBusSender GetOrCreateSender(ServiceBusSubscriberSettings subscription)
    {
        var key = $"{subscription.EffectiveConnectionString}:{subscription.DestinationName}";

        return _senders.GetOrAdd(
            key,
            _ =>
            {
                var client = GetOrCreateClient(subscription);
                return client.CreateSender(subscription.DestinationName);
            }
        );
    }

    private ServiceBusClient GetOrCreateClient(ServiceBusSubscriberSettings subscription)
    {
        return _clients.GetOrAdd(
            subscription.EffectiveConnectionString,
            connectionString =>
            {
                logger.LogDebug(
                    "Creating Service Bus client for subscription '{SubscriberName}'",
                    subscription.Name
                );

                return new ServiceBusClient(connectionString);
            }
        );
    }
}
