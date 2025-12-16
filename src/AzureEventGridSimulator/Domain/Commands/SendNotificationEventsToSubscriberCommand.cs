using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationEventsToSubscriberCommand : IRequest
{
    public SendNotificationEventsToSubscriberCommand(
        SimulatorEvent[] events,
        TopicSettings topic,
        EventSchema inputSchema
    )
    {
        Events = events;
        Topic = topic;
        InputSchema = inputSchema;
    }

    public TopicSettings Topic { get; }

    public SimulatorEvent[] Events { get; }

    public EventSchema InputSchema { get; }
}
