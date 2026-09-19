using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.ServiceBus;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
///     Delivers events to Azure Service Bus queues and topics.
/// </summary>
public class ServiceBusEventDeliveryService(
    ILogger<ServiceBusEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory,
    DeliveryPropertyResolver propertyResolver
) : IEventDeliveryService, IAsyncDisposable
{
    // Lazy values guarantee a single client/sender per key even when GetOrAdd factories race;
    // a lost race would otherwise leak an undisposed client holding a live AMQP connection.
    private readonly ConcurrentDictionary<string, Lazy<ServiceBusClient>> _clients = new();
    private readonly ConcurrentDictionary<string, Lazy<ServiceBusSender>> _senders = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values.Where(s => s.IsValueCreated))
        {
            await sender.Value.DisposeAsync();
        }

        foreach (var client in _clients.Values.Where(c => c.IsValueCreated))
        {
            await client.Value.DisposeAsync();
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

            var deliverySchema = delivery.DeliverySchema;
            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // Serialize as a single event (matches Azure Event Grid to Service Bus behavior)
            var json = formatter.SerializeSingle(delivery.Event);

            // Get or create the sender
            var sender = GetOrCreateSender(subscription);

            // Create the message
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
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
                message.ApplicationProperties[name] = value;
            }

            // Add standard Event Grid headers as application properties
            message.ApplicationProperties[Constants.AegEventTypeHeader] =
                Constants.NotificationEventType;
            message.ApplicationProperties[Constants.AegSubscriptionNameHeader] =
                subscription.Name.ToUpperInvariant();
            message.ApplicationProperties[Constants.AegDeliveryCountHeader] =
                delivery.AttemptCount + 1;

            if (deliverySchema == EventSchema.EventGridSchema)
            {
                message.ApplicationProperties[Constants.AegDataVersionHeader] =
                    delivery.Event.DataVersion ?? "";
                message.ApplicationProperties[Constants.AegMetadataVersionHeader] = "1";
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

    private ServiceBusSender GetOrCreateSender(ServiceBusSubscriberSettings subscription)
    {
        var key = $"{subscription.EffectiveConnectionString}:{subscription.DestinationName}";

        return _senders
            .GetOrAdd(
                key,
                _ => new Lazy<ServiceBusSender>(() =>
                {
                    var client = GetOrCreateClient(subscription);
                    return client.CreateSender(subscription.DestinationName);
                })
            )
            .Value;
    }

    private ServiceBusClient GetOrCreateClient(ServiceBusSubscriberSettings subscription)
    {
        var connectionString =
            subscription.EffectiveConnectionString
            ?? throw new InvalidOperationException(
                $"No connection string available for subscription '{subscription.Name}'"
            );

        return _clients
            .GetOrAdd(
                connectionString,
                cs => new Lazy<ServiceBusClient>(() =>
                {
                    logger.LogDebug(
                        "Creating Service Bus client for subscription '{SubscriberName}'",
                        subscription.Name
                    );

                    return new ServiceBusClient(cs);
                })
            )
            .Value;
    }
}
