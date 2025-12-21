using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Commands;

/// <summary>
/// Handles the SendNotificationEventsToSubscriberCommand by enqueueing events for delivery.
/// Events are processed by the RetryDeliveryBackgroundService which handles delivery and retries.
/// </summary>
// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToSubscriberCommandHandler(
    IDeliveryQueue deliveryQueue,
    ILogger<SendNotificationEventsToSubscriberCommandHandler> logger
) : IRequestHandler<SendNotificationEventsToSubscriberCommand>
{
    public Task Handle(
        SendNotificationEventsToSubscriberCommand request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "{EventCount} event(s) received on topic '{TopicName}' (Schema: {Schema})",
            request.Events.Length,
            request.Topic.Name,
            request.InputSchema
        );

        // Enrich events with topic information
        EnrichEvents(request.Events, request.Topic.Name);

        var allSubscribers = request.Topic.Subscribers.All.ToList();

        logger.LogDebug(
            "Topic '{TopicName}' has {Count} subscriber(s): {Subscribers}",
            request.Topic.Name,
            allSubscribers.Count,
            string.Join(", ", allSubscribers.Select(s => $"{s.Name} ({s.SubscriberType})"))
        );

        if (allSubscribers.Count == 0)
        {
            logger.LogWarning(
                "'{TopicName}' has no subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
            return Task.CompletedTask;
        }

        if (allSubscribers.All(o => o.Disabled))
        {
            logger.LogWarning(
                "'{TopicName}' has no enabled subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
            return Task.CompletedTask;
        }

        // Log events that are filtered out by all subscribers
        var eventsFilteredOutByAllSubscribers = request
            .Events.Where(e => allSubscribers.All(s => !s.Filter.AcceptsEvent(e)))
            .ToArray();

        foreach (var filteredEvent in eventsFilteredOutByAllSubscribers)
        {
            logger.LogWarning(
                "All subscribers of topic '{TopicName}' filtered out event {EventId}",
                request.Topic.Name,
                filteredEvent.Id
            );
        }

        // Enqueue events for each subscriber
        var enqueuedCount = 0;

        foreach (var subscriber in allSubscribers)
        {
            if (subscriber.Disabled)
            {
                logger.LogDebug(
                    "Skipping disabled subscriber '{SubscriberName}' on topic '{TopicName}'",
                    subscriber.Name,
                    request.Topic.Name
                );
                continue;
            }

            // Check HTTP subscriber validation status
            if (
                subscriber is HttpSubscriberSettings httpSubscriber
                && !httpSubscriber.DisableValidation
                && httpSubscriber.ValidationStatus
                    != SubscriptionValidationStatus.ValidationSuccessful
            )
            {
                logger.LogWarning(
                    "Subscription '{SubscriberName}' on topic '{TopicName}' can't receive events. It's still pending validation",
                    subscriber.Name,
                    request.Topic.Name
                );
                continue;
            }

            foreach (var evt in request.Events)
            {
                if (!subscriber.Filter.AcceptsEvent(evt))
                {
                    logger.LogDebug(
                        "Event {EventId} filtered out for subscriber '{SubscriberName}'",
                        evt.Id,
                        subscriber.Name
                    );
                    continue;
                }

                // Create pending delivery and enqueue
                var pendingDelivery = new PendingDelivery
                {
                    Event = evt,
                    Subscriber = subscriber,
                    Topic = request.Topic,
                    InputSchema = request.InputSchema,
                };

                deliveryQueue.Enqueue(pendingDelivery);
                enqueuedCount++;
            }
        }

        if (enqueuedCount > 0)
        {
            logger.LogDebug(
                "Enqueued {Count} event delivery(ies) for topic '{TopicName}'",
                enqueuedCount,
                request.Topic.Name
            );
        }

        return Task.CompletedTask;
    }

    private static void EnrichEvents(SimulatorEvent[] events, string topicName)
    {
        var topicPath =
            $"/subscriptions/{Guid.Empty:D}/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/{topicName}";

        foreach (var evt in events)
        {
            if (evt.Schema == EventSchema.EventGridSchema && evt.EventGridEvent != null)
            {
                evt.EventGridEvent.Topic = topicPath;
                evt.EventGridEvent.MetadataVersion = "1";
            }
            else if (evt.Schema == EventSchema.CloudEventV1_0 && evt.CloudEvent != null)
            {
                // CloudEvents use 'source' which is already set
                // Optionally set it to the topic path if not already set
                if (string.IsNullOrEmpty(evt.CloudEvent.Source))
                {
                    evt.CloudEvent.Source = topicPath;
                }
            }
        }
    }
}
