using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Delivers events to Azure Storage Queues.
/// </summary>
public class StorageQueueEventDeliveryService(
    ILogger<StorageQueueEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory
) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, QueueClient> _clients = new();

    /// <summary>
    /// Sends an event to a Storage Queue subscriber.
    /// </summary>
    public async Task SendAsync(
        StorageQueueSubscriberSettings subscription,
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
                    "Storage Queue subscription '{SubscriberName}' on topic '{TopicName}' is disabled",
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

            // Get or create the queue client (creates queue if it doesn't exist)
            var client = await GetOrCreateClientAsync(subscription);

            // Base64 encode the JSON (matches Azure Event Grid behavior)
            var messageText = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

            // Send the message
            await client.SendMessageAsync(messageText);

            logger.LogDebug(
                "Event {EventId} sent to Storage Queue '{QueueName}' via subscription '{SubscriberName}' on topic '{TopicName}'",
                evt.Id,
                subscription.QueueName,
                subscription.Name,
                topic.Name
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send event {EventId} to Storage Queue '{QueueName}' via subscription '{SubscriberName}'",
                evt.Id,
                subscription.QueueName,
                subscription.Name
            );
        }
    }

    private async Task<QueueClient> GetOrCreateClientAsync(
        StorageQueueSubscriberSettings subscription
    )
    {
        var key = $"{subscription.EffectiveConnectionString}:{subscription.QueueName}";

        if (_clients.TryGetValue(key, out var existingClient))
        {
            return existingClient;
        }

        logger.LogDebug(
            "Creating Storage Queue client for subscription '{SubscriberName}' (Queue: '{QueueName}')",
            subscription.Name,
            subscription.QueueName
        );

        var client = new QueueClient(
            subscription.EffectiveConnectionString,
            subscription.QueueName
        );

        // Create the queue if it doesn't exist (useful for local development with Azurite)
        await client.CreateIfNotExistsAsync();

        _clients.TryAdd(key, client);
        return client;
    }

    public ValueTask DisposeAsync()
    {
        // QueueClient doesn't require explicit disposal, but we clear the cache for cleanup
        _clients.Clear();
        return ValueTask.CompletedTask;
    }
}
