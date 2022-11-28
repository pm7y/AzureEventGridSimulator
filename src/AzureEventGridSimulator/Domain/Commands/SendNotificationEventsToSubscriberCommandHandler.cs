using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToSubscriberCommandHandler : AsyncRequestHandler<SendNotificationEventsToSubscriberCommand>
{
    private readonly IMediator _mediator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SendNotificationEventsToSubscriberCommandHandler> _logger;

    public SendNotificationEventsToSubscriberCommandHandler(IMediator mediator, IHttpClientFactory httpClientFactory, ILogger<SendNotificationEventsToSubscriberCommandHandler> logger)
    {
        _mediator = mediator;
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

        if (!request.Topic.Subscribers.AllSubscriptions.Any())
        {
            _logger.LogWarning("'{TopicName}' has no subscribers so {EventCount} event(s) could not be forwarded", request.Topic.Name, request.Events.Length);
        }
        else if (request.Topic.Subscribers.AllSubscriptions.All(o => o.Disabled))
        {
            _logger.LogWarning("'{TopicName}' has no enabled subscribers so {EventCount} event(s) could not be forwarded", request.Topic.Name, request.Events.Length);
        }
        else
        {
            var eventsFilteredOutByAllSubscribers = request.Events
                                                           .Where(e => request.Topic.Subscribers.AllSubscriptions.All(s => !s.Filter.AcceptsEvent(e)))
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
                foreach (var subscription in request.Topic.Subscribers.Http)
                {
#pragma warning disable 4014
                    _mediator.Send(new SendNotificationEventsToSpecializedSubscriberCommand<HttpSubscriptionSettings>(subscription, request.Events, request.Topic.Name));
#pragma warning restore 4014
                }
            }
        }

        return Task.CompletedTask;
    }
}
