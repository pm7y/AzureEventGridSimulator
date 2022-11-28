using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationEventsToSpecializedSubscriberCommand<T> : IRequest
    where T : BaseSubscriptionSettings
{
    public SendNotificationEventsToSpecializedSubscriberCommand(T subscription, EventGridEvent[] events, string topicName)
    {
        Subscription = subscription;
        Events = events;
        TopicName = topicName;
    }

    public T Subscription { get; }
    public string TopicName { get; }
    public EventGridEvent[] Events { get; }
}
