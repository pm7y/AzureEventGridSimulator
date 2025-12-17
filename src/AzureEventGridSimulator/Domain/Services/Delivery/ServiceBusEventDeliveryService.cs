using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Delivers events to Azure Service Bus queues and topics.
/// </summary>
public class ServiceBusEventDeliveryService : IAsyncDisposable
{
    private readonly ILogger<ServiceBusEventDeliveryService> _logger;
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly DeliveryPropertyResolver _propertyResolver;
    private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusEventDeliveryService(
        ILogger<ServiceBusEventDeliveryService> logger,
        EventSchemaFormatterFactory formatterFactory,
        DeliveryPropertyResolver propertyResolver
    )
    {
        _logger = logger;
        _formatterFactory = formatterFactory;
        _propertyResolver = propertyResolver;
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
                _logger.LogWarning(
                    "Service Bus subscription '{SubscriberName}' on topic '{TopicName}' is disabled",
                    subscription.Name,
                    topic.Name
                );
                return;
            }

            // Determine the delivery schema
            var deliverySchema = subscription.DeliverySchema ?? topic.OutputSchema ?? inputSchema;
            var formatter = _formatterFactory.GetFormatter(deliverySchema);

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
            var properties = _propertyResolver.ResolveProperties(subscription.Properties, evt);
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

            _logger.LogDebug(
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
            _logger.LogError(
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
                _logger.LogDebug(
                    "Creating Service Bus client for subscription '{SubscriberName}'",
                    subscription.Name
                );

                return new ServiceBusClient(connectionString);
            }
        );
    }

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
}
