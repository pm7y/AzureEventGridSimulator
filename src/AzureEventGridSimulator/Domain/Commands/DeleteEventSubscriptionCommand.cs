using AzureEventGridSimulator.Infrastructure.Mediator;

namespace AzureEventGridSimulator.Domain.Commands;

public class DeleteEventSubscriptionCommand(string topicName, string eventSubscriptionName)
    : IRequest<bool>
{
    public string TopicName { get; } = topicName;

    public string EventSubscriptionName { get; } = eventSubscriptionName;
}
