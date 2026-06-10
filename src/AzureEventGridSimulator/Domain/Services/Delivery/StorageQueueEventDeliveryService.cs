using System.Collections.Concurrent;
using System.Text;
using Azure;
using Azure.Storage.Queues;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
///     Delivers events to Azure Storage Queues.
/// </summary>
public class StorageQueueEventDeliveryService(
    ILogger<StorageQueueEventDeliveryService> logger,
    EventSchemaFormatterFactory formatterFactory
) : IEventDeliveryService, IAsyncDisposable
{
    // Lazy task values guarantee the client (and its CreateIfNotExistsAsync round-trip) is
    // created once per queue even when concurrent deliveries race on the same key.
    private readonly ConcurrentDictionary<string, Lazy<Task<QueueClient>>> _clients = new();

    public ValueTask DisposeAsync()
    {
        // QueueClient doesn't require explicit disposal, but we clear the cache for cleanup
        _clients.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DeliveryResult> DeliverAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        if (delivery.Subscriber is not StorageQueueSubscriberSettings subscription)
        {
            return new DeliveryResult(
                false,
                DeliveryOutcome.StorageQueueError,
                ErrorMessage: "Invalid subscriber type for Storage Queue delivery"
            );
        }

        try
        {
            if (subscription.Disabled)
            {
                return new DeliveryResult(
                    false,
                    DeliveryOutcome.StorageQueueError,
                    ErrorMessage: "Subscription is disabled"
                );
            }

            // Determine the delivery schema
            var deliverySchema =
                subscription.DeliverySchema ?? delivery.Topic.OutputSchema ?? delivery.InputSchema;
            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // Serialize the event
            var json = formatter.Serialize(delivery.Event);

            // Get or create the queue client (creates queue if it doesn't exist)
            var client = await GetOrCreateClientAsync(subscription);

            // Base64 encode the JSON (matches Azure Event Grid behavior)
            var messageText = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            // Send the message
            await client.SendMessageAsync(messageText, cancellationToken);

            logger.LogDebug(
                "Event {EventId} sent to Storage Queue '{QueueName}' via subscription '{SubscriberName}'",
                delivery.Event.Id,
                subscription.QueueName,
                subscription.Name
            );

            return new DeliveryResult(true, DeliveryOutcome.Success);
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(
                ex,
                "Storage Queue error sending event {EventId} to queue '{QueueName}'",
                delivery.Event.Id,
                subscription.QueueName
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.StorageQueueError,
                ex.Status,
                ex.Message
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
                "Unexpected error sending event {EventId} to Storage Queue",
                delivery.Event.Id
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.StorageQueueError,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    ///     Sends an event to a Storage Queue subscriber.
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
            var messageText = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

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

        var lazyClient = _clients.GetOrAdd(
            key,
            _ => new Lazy<Task<QueueClient>>(() => CreateClientAsync(subscription))
        );

        try
        {
            return await lazyClient.Value;
        }
        catch
        {
            // Don't cache a failed creation (e.g. Azurite not yet running) - evict so the
            // next delivery attempt retries. Remove only our own entry to avoid evicting a
            // newer replacement added by another thread.
            _clients.TryRemove(new KeyValuePair<string, Lazy<Task<QueueClient>>>(key, lazyClient));
            throw;
        }
    }

    private async Task<QueueClient> CreateClientAsync(StorageQueueSubscriberSettings subscription)
    {
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

        return client;
    }
}
