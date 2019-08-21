using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands
{

    public class SendNotificationEventsToSubscriberCommand : IRequest
    {
        public SendNotificationEventsToSubscriberCommand(EventGridEvent[] events, TopicSettings topic)
        {
            Events = events;
            Topic = topic;
        }

        public TopicSettings Topic { get; set; }

        public EventGridEvent[] Events { get; }
    }
}
