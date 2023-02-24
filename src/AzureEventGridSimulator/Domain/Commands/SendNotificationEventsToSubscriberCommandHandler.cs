namespace AzureEventGridSimulator.Domain.Commands;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

// ReSharper disable once UnusedMember.Global
public abstract class SendNotificationEventsToSubscriberCommandHandler<TEvent> : AsyncRequestHandler<SendNotificationEventsToSubscriberCommand<TEvent>>
    where TEvent : IEvent
{
    private readonly IMediator _mediator;
    private readonly ILogger _logger;

    protected SendNotificationEventsToSubscriberCommandHandler(ILogger logger, IMediator mediator)
    {
        _mediator = mediator;
        _logger = logger;
    }

    protected override async Task Handle(SendNotificationEventsToSubscriberCommand<TEvent> request, CancellationToken cancellationToken)
    {
        Task SendAsync<TSubscription>(TSubscription[] subscriptions)
            where TSubscription : BaseSubscriptionSettings
        {
            return Task.WhenAll(subscriptions.Select(subscription => _mediator.Send(new SendNotificationEventsToSpecializedSubscriberCommand<TSubscription, TEvent>(subscription, request.Events, request.Topic.Name), cancellationToken)));
        }

        _logger.LogInformation("{EventCount} event(s) received on topic '{TopicName}'", request.Events.Length, request.Topic.Name);

        Prepare(request.Topic, request.Events);

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
                await Task.WhenAll(
                    SendAsync(request.Topic.Subscribers.Http),
                    SendAsync(request.Topic.Subscribers.ServiceBus));
            }
        }
    }

    protected abstract void Prepare(TopicSettings topic, TEvent[] events);
}
