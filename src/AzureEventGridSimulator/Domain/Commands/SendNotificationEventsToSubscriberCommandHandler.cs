using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToSubscriberCommandHandler
    : IRequestHandler<SendNotificationEventsToSubscriberCommand>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SendNotificationEventsToSubscriberCommandHandler> _logger;
    private readonly EventSchemaFormatterFactory _formatterFactory;

    public SendNotificationEventsToSubscriberCommandHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<SendNotificationEventsToSubscriberCommandHandler> logger,
        EventSchemaFormatterFactory formatterFactory
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _formatterFactory = formatterFactory;
    }

    public Task Handle(
        SendNotificationEventsToSubscriberCommand request,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "{EventCount} event(s) received on topic '{TopicName}' (Schema: {Schema})",
            request.Events.Length,
            request.Topic.Name,
            request.InputSchema
        );

        // Enrich events with topic information
        EnrichEvents(request.Events, request.Topic.Name);

        if (!request.Topic.Subscribers.Any())
        {
            _logger.LogWarning(
                "'{TopicName}' has no subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
        }
        else if (request.Topic.Subscribers.All(o => o.Disabled))
        {
            _logger.LogWarning(
                "'{TopicName}' has no enabled subscribers so {EventCount} event(s) could not be forwarded",
                request.Topic.Name,
                request.Events.Length
            );
        }
        else
        {
            var eventsFilteredOutByAllSubscribers = request
                .Events.Where(e => request.Topic.Subscribers.All(s => !s.Filter.AcceptsEvent(e)))
                .ToArray();

            if (eventsFilteredOutByAllSubscribers.Any())
            {
                foreach (var eventFilteredOutByAllSubscribers in eventsFilteredOutByAllSubscribers)
                {
                    _logger.LogWarning(
                        "All subscribers of topic '{TopicName}' filtered out event {EventId}",
                        request.Topic.Name,
                        eventFilteredOutByAllSubscribers.Id
                    );
                }
            }
            else
            {
                foreach (var subscription in request.Topic.Subscribers)
                {
#pragma warning disable 4014
                    SendToSubscriber(
                        subscription,
                        request.Events,
                        request.Topic,
                        request.InputSchema
                    );
#pragma warning restore 4014
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

    private async Task SendToSubscriber(
        SubscriptionSettings subscription,
        IEnumerable<SimulatorEvent> events,
        TopicSettings topic,
        EventSchema inputSchema
    )
    {
        try
        {
            if (subscription.Disabled)
            {
                _logger.LogWarning(
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
                _logger.LogWarning(
                    "Subscription '{SubscriberName}' on topic '{TopicName}' can't receive events. It's still pending validation",
                    subscription.Name,
                    topic.Name
                );
                return;
            }

            _logger.LogDebug(
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
                _logger.LogWarning(
                    "CloudEvents input to Event Grid output conversion is NOT supported by Azure Event Grid. "
                        + "Subscriber '{SubscriberName}' on topic '{TopicName}' has incompatible schema configuration. "
                        + "This will work in the simulator but will fail with actual Azure Event Grid.",
                    subscription.Name,
                    topic.Name
                );
            }

            var formatter = _formatterFactory.GetFormatter(deliverySchema);

            // "Event Grid sends the events to subscribers in an array that has a single event. This behaviour may change in the future."
            // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
            foreach (var evt in events)
            {
                if (subscription.Filter.AcceptsEvent(evt))
                {
                    var json = formatter.Serialize(evt);

                    using var content = new StringContent(json, Encoding.UTF8);
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(formatter.ContentType);
                    var httpClient = _httpClientFactory.CreateClient();

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
                    _logger.LogDebug(
                        "Event {EventId} filtered out for subscriber '{SubscriberName}'",
                        evt.Id,
                        subscription.Name
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send to subscriber '{SubscriberName}'",
                subscription.Name
            );
        }
    }

    private void LogResult(
        Task<HttpResponseMessage> task,
        SimulatorEvent evt,
        SubscriptionSettings subscription,
        string topicName
    )
    {
        if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "Event {EventId} sent to subscriber '{SubscriberName}' on topic '{TopicName}' successfully",
                evt.Id,
                subscription.Name,
                topicName
            );
        }
        else
        {
            _logger.LogError(
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
