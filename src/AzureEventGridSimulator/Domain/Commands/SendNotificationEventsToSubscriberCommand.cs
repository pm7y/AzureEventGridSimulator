using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationEventsToSubscriberCommand<T> : IRequest
    where T: IEvent
{
    public SendNotificationEventsToSubscriberCommand(T[] events, TopicSettings topic)
    {
        Events = events;
        Topic = topic;
    }

    public TopicSettings Topic { get; }

    public T[] Events { get; }
}
