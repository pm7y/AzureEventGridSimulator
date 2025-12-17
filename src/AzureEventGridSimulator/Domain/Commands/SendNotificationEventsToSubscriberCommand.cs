using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Commands;

public class SendNotificationEventsToSubscriberCommand(
    SimulatorEvent[] events,
    TopicSettings topic,
    EventSchema inputSchema
) : IRequest
{
    public TopicSettings Topic { get; } = topic;

    public SimulatorEvent[] Events { get; } = events;

    public EventSchema InputSchema { get; } = inputSchema;
}
