using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToSubscriberCommandHandler : AsyncRequestHandler<SendNotificationEventsToSubscriberCommand>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SendNotificationEventsToSubscriberCommandHandler> _logger;

    public SendNotificationEventsToSubscriberCommandHandler(IHttpClientFactory httpClientFactory, ILogger<SendNotificationEventsToSubscriberCommandHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override Task Handle(SendNotificationEventsToSubscriberCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{EventCount} event(s) received on topic '{TopicName}'", request.Events.Length, request.Topic.Name);

        foreach (var eventGridEvent in request.Events)
        {
            eventGridEvent.Topic = $"/subscriptions/{Guid.Empty:D}/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/{request.Topic.Name}";
            eventGridEvent.MetadataVersion = "1";
        }

        if (!request.Topic.Subscribers.Any())
        {
            _logger.LogWarning("'{TopicName}' has no subscribers so {EventCount} event(s) could not be forwarded", request.Topic.Name, request.Events.Length);
        }
        else if (request.Topic.Subscribers.All(o => o.Disabled))
        {
            _logger.LogWarning("'{TopicName}' has no enabled subscribers so {EventCount} event(s) could not be forwarded", request.Topic.Name, request.Events.Length);
        }
        else
        {
            var eventsFilteredOutByAllSubscribers = request.Events
                                                           .Where(e => request.Topic.Subscribers.All(s => !s.Filter.AcceptsEvent(e)))
                                                           .ToArray();

            if (eventsFilteredOutByAllSubscribers.Any())
            {
                foreach (var eventFilteredOutByAllSubscribers in eventsFilteredOutByAllSubscribers)
                {
                    _logger.LogWarning("All subscribers of topic '{TopicName}' filtered out event {EventId}",
                                       request.Topic.Name,
                                       eventFilteredOutByAllSubscribers.Id);
                }
            }
            else
            {
                foreach (var subscription in request.Topic.Subscribers)
                {
#pragma warning disable 4014
                    SendToSubscriber(subscription, request.Events, request.Topic.Name);
#pragma warning restore 4014
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task SendToSubscriber(SubscriptionSettings subscription, IEnumerable<EventGridEvent> events, string topicName)
    {
        try
        {
            if (subscription.Disabled)
            {
                _logger.LogWarning("Subscription '{SubscriberName}' on topic '{TopicName}' is disabled and so Notification was skipped", subscription.Name, topicName);
                return;
            }

            if (!subscription.DisableValidation &&
                subscription.ValidationStatus != SubscriptionValidationStatus.ValidationSuccessful)
            {
                _logger.LogWarning("Subscription '{SubscriberName}' on topic '{TopicName}' can't receive events. It's still pending validation", subscription.Name, topicName);
                return;
            }

            _logger.LogDebug("Sending to subscriber '{SubscriberName}' on topic '{TopicName}'", subscription.Name, topicName);

            // "Event Grid sends the events to subscribers in an array that has a single event. This behaviour may change in the future."
            // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
            foreach (var evt in events)
            {
                if (subscription.Filter.AcceptsEvent(evt))
                {
                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegSubscriptionNameHeader, subscription.Name.ToUpperInvariant());
                    httpClient.DefaultRequestHeaders.Add(Constants.AegDataVersionHeader, evt.DataVersion);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegMetadataVersionHeader, evt.MetadataVersion);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    await httpClient.PostAsync(subscription.Endpoint, content)
                                    .ContinueWith(t => LogResult(t, evt, subscription, topicName));
                }
                else
                {
                    _logger.LogDebug("Event {EventId} filtered out for subscriber '{SubscriberName}'", evt.Id, subscription.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send to subscriber '{SubscriberName}'", subscription.Name);
        }
    }

    private void LogResult(Task<HttpResponseMessage> task, EventGridEvent evt, SubscriptionSettings subscription, string topicName)
    {
        if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode)
        {
            _logger.LogDebug("Event {EventId} sent to subscriber '{SubscriberName}' on topic '{TopicName}' successfully", evt.Id, subscription.Name, topicName);
        }
        else
        {
            _logger.LogError(task.Exception?.GetBaseException(),
                             "Failed to send event {EventId} to subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'",
                             evt.Id,
                             subscription.Name,
                             task.Status.ToString(),
                             task.Result?.ReasonPhrase);
        }
    }
}
