using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationCloudEventsToSubscriberCommand : IRequest
{
    public SendNotificationCloudEventsToSubscriberCommand(CloudEventGridEvent[] events, TopicSettings topic)
    {
        Events = events;
        Topic = topic;
    }

    public TopicSettings Topic { get; }

    public CloudEventGridEvent[] Events { get; }
}
