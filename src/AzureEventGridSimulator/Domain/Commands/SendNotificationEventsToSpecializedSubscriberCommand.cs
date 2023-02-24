using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationEventsToSpecializedSubscriberCommand<TSubscription, TEvent> : IRequest
    where TSubscription : BaseSubscriptionSettings
{
    public SendNotificationEventsToSpecializedSubscriberCommand(TSubscription subscription, TEvent[] events, string topicName)
    {
        Subscription = subscription;
        Events = events;
        TopicName = topicName;
    }

    public TSubscription Subscription { get; }
    public string TopicName { get; }
    public TEvent[] Events { get; }
}
