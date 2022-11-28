using System;
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
public class SendNotificationEventsToHttpSubscriberCommandHandler : AsyncRequestHandler<SendNotificationEventsToSpecializedSubscriberCommand<HttpSubscriptionSettings>>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public SendNotificationEventsToHttpSubscriberCommandHandler(IHttpClientFactory httpClientFactory, ILogger<SendNotificationEventsToHttpSubscriberCommandHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task Handle(SendNotificationEventsToSpecializedSubscriberCommand<HttpSubscriptionSettings> request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Subscription.Disabled)
            {
                _logger.LogWarning("HTTP subscription '{SubscriberName}' on topic '{TopicName}' is disabled and so Notification was skipped", request.Subscription.Name, request.TopicName);
                return;
            }

            if (!request.Subscription.DisableValidation &&
                request.Subscription.ValidationStatus != SubscriptionValidationStatus.ValidationSuccessful)
            {
                _logger.LogWarning("HTTP subscription '{SubscriberName}' on topic '{TopicName}' can't receive events. It's still pending validation", request.Subscription.Name, request.TopicName);
                return;
            }

            _logger.LogDebug("Sending to HTTP subscriber '{SubscriberName}' on topic '{TopicName}'", request.Subscription.Name, request.TopicName);

            // "Event Grid sends the events to subscribers in an array that has a single event. This behaviour may change in the future."
            // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
            foreach (var evt in request.Events)
            {
                if (request.Subscription.Filter.AcceptsEvent(evt))
                {
                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegSubscriptionNameHeader, request.Subscription.Name.ToUpperInvariant());
                    httpClient.DefaultRequestHeaders.Add(Constants.AegDataVersionHeader, evt.DataVersion);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegMetadataVersionHeader, evt.MetadataVersion);
                    httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    await httpClient.PostAsync(request.Subscription.Endpoint, content)
                                    .ContinueWith(t => LogResult(t, evt, request.Subscription, request.TopicName));
                }
                else
                {
                    _logger.LogDebug("Event {EventId} filtered out for HTTP subscriber '{SubscriberName}'", evt.Id, request.Subscription.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send to HTTP subscriber '{SubscriberName}'", request.Subscription.Name);
        }
    }

    private void LogResult(Task<HttpResponseMessage> task, EventGridEvent evt, HttpSubscriptionSettings subscription, string topicName)
    {
        if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode)
        {
            _logger.LogDebug("Event {EventId} sent to HTTP subscriber '{SubscriberName}' on topic '{TopicName}' successfully", evt.Id, subscription.Name, topicName);
        }
        else
        {
            _logger.LogError(task.Exception?.GetBaseException(),
                             "Failed to send event {EventId} to HTTP subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'",
                             evt.Id,
                             subscription.Name,
                             task.Status.ToString(),
                             task.Result?.ReasonPhrase);
        }
    }
}
