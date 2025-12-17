using System.Net.Http.Headers;
using System.Text;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToSubscriberCommandHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<SendNotificationEventsToSubscriberCommandHandler> logger,
    EventSchemaFormatterFactory formatterFactory,
    ServiceBusEventDeliveryService serviceBusDeliveryService,
    StorageQueueEventDeliveryService storageQueueDeliveryService
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

        if (allSubscribers.Count == 0)
        {
            logger.LogWarning(
                "'{TopicName}' has no subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
        }
        else if (allSubscribers.All(o => o.Disabled))
        {
            logger.LogWarning(
                "'{TopicName}' has no enabled subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
        }
        else
        {
            var eventsFilteredOutByAllSubscribers = request
                .Events.Where(e => allSubscribers.All(s => !s.Filter.AcceptsEvent(e)))
                .ToArray();

            if (eventsFilteredOutByAllSubscribers.Length != 0)
            {
                foreach (var eventFilteredOutByAllSubscribers in eventsFilteredOutByAllSubscribers)
                {
                    logger.LogWarning(
                        "All subscribers of topic '{TopicName}' filtered out event {EventId}",
                        request.Topic.Name,
                        eventFilteredOutByAllSubscribers.Id
                    );
                }
            }
            else
            {
                // Send to HTTP subscribers
                foreach (var subscription in request.Topic.Subscribers.HttpSubscribers)
                {
#pragma warning disable 4014
                    SendToHttpSubscriber(
                        subscription,
                        request.Events,
                        request.Topic,
                        request.InputSchema
                    );
#pragma warning restore 4014
                }

                // Send to Service Bus subscribers
                foreach (var subscription in request.Topic.Subscribers.ServiceBusSubscribers)
                {
                    foreach (var evt in request.Events)
                    {
                        if (subscription.Filter.AcceptsEvent(evt))
                        {
#pragma warning disable 4014
                            serviceBusDeliveryService.SendAsync(
                                subscription,
                                evt,
                                request.Topic,
                                request.InputSchema
                            );
#pragma warning restore 4014
                        }
                        else
                        {
                            logger.LogDebug(
                                "Event {EventId} filtered out for Service Bus subscriber '{SubscriberName}'",
                                evt.Id,
                                subscription.Name
                            );
                        }
                    }
                }

                // Send to Storage Queue subscribers
                foreach (var subscription in request.Topic.Subscribers.StorageQueueSubscribers)
                {
                    foreach (var evt in request.Events)
                    {
                        if (subscription.Filter.AcceptsEvent(evt))
                        {
#pragma warning disable 4014
                            storageQueueDeliveryService.SendAsync(
                                subscription,
                                evt,
                                request.Topic,
                                request.InputSchema
                            );
#pragma warning restore 4014
                        }
                        else
                        {
                            logger.LogDebug(
                                "Event {EventId} filtered out for Storage Queue subscriber '{SubscriberName}'",
                                evt.Id,
                                subscription.Name
                            );
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private void EnrichEvents(SimulatorEvent[] events, string topicName)
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

    private async Task SendToHttpSubscriber(
        HttpSubscriberSettings subscription,
        IEnumerable<SimulatorEvent> events,
        TopicSettings topic,
        EventSchema inputSchema
    )
    {
        try
        {
            if (subscription.Disabled)
            {
                logger.LogWarning(
                    "Subscription '{SubscriberName}' on topic '{TopicName}' is disabled and so Notification was skipped",
                    subscription.Name,
                    topic.Name
                );
                return;
            }

            if (
                !subscription.DisableValidation
                && subscription.ValidationStatus
                    != SubscriptionValidationStatus.ValidationSuccessful
            )
            {
                logger.LogWarning(
                    "Subscription '{SubscriberName}' on topic '{TopicName}' can't receive events. It's still pending validation",
                    subscription.Name,
                    topic.Name
                );
                return;
            }

            logger.LogDebug(
                "Sending to subscriber '{SubscriberName}' on topic '{TopicName}'",
                subscription.Name,
                topic.Name
            );

            // Determine the delivery schema: subscriber override > topic output > input schema
            var deliverySchema = subscription.DeliverySchema ?? topic.OutputSchema ?? inputSchema;

            // IMPORTANT: Azure Event Grid does NOT support CloudEvents input -> Event Grid output conversion
            // This simulator allows it for testing flexibility, but logs a warning
            if (
                inputSchema == EventSchema.CloudEventV1_0
                && deliverySchema == EventSchema.EventGridSchema
            )
            {
                logger.LogWarning(
                    "CloudEvents input to Event Grid output conversion is NOT supported by Azure Event Grid. "
                        + "Subscriber '{SubscriberName}' on topic '{TopicName}' has incompatible schema configuration. "
                        + "This will work in the simulator but will fail with actual Azure Event Grid",
                    subscription.Name,
                    topic.Name
                );
            }

            var formatter = formatterFactory.GetFormatter(deliverySchema);

            // "Event Grid sends the events to subscribers in an array that has a single event. This behaviour may change in the future."
            // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
            foreach (var evt in events)
            {
                if (subscription.Filter.AcceptsEvent(evt))
                {
                    var json = formatter.Serialize(evt);

                    using var content = new StringContent(json, Encoding.UTF8);
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(formatter.ContentType);
                    var httpClient = httpClientFactory.CreateClient();

                    // Add standard Event Grid headers
                    httpClient.DefaultRequestHeaders.Add(
                        Constants.AegEventTypeHeader,
                        Constants.NotificationEventType
                    );
                    httpClient.DefaultRequestHeaders.Add(
                        Constants.AegSubscriptionNameHeader,
                        subscription.Name.ToUpperInvariant()
                    );
                    httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries

                    // Add schema-specific headers
                    if (deliverySchema == EventSchema.EventGridSchema)
                    {
                        httpClient.DefaultRequestHeaders.Add(
                            Constants.AegDataVersionHeader,
                            evt.DataVersion ?? ""
                        );
                        httpClient.DefaultRequestHeaders.Add(
                            Constants.AegMetadataVersionHeader,
                            "1"
                        );
                    }

                    // Add any additional headers from the formatter
                    foreach (var header in formatter.GetHeaders(evt))
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }

                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    await httpClient
                        .PostAsync(subscription.Endpoint, content)
                        .ContinueWith(t => LogResult(t, evt, subscription, topic.Name));
                }
                else
                {
                    logger.LogDebug(
                        "Event {EventId} filtered out for subscriber '{SubscriberName}'",
                        evt.Id,
                        subscription.Name
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send to subscriber '{SubscriberName}'",
                subscription.Name
            );
        }
    }

    private void LogResult(
        Task<HttpResponseMessage> task,
        SimulatorEvent evt,
        HttpSubscriberSettings subscription,
        string topicName
    )
    {
        if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode)
        {
            logger.LogDebug(
                "Event {EventId} sent to subscriber '{SubscriberName}' on topic '{TopicName}' successfully",
                evt.Id,
                subscription.Name,
                topicName
            );
        }
        else
        {
            logger.LogError(
                task.Exception?.GetBaseException(),
                "Failed to send event {EventId} to subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'",
                evt.Id,
                subscription.Name,
                task.Status.ToString(),
                task.Result?.ReasonPhrase
            );
        }
    }
}
