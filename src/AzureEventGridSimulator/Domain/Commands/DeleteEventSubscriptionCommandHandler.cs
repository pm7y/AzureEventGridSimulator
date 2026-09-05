using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class DeleteEventSubscriptionCommandHandler(
    SimulatorSettings simulatorSettings,
    ILogger<DeleteEventSubscriptionCommandHandler> logger
) : IRequestHandler<DeleteEventSubscriptionCommand, bool>
{
    public Task<bool> Handle(
        DeleteEventSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        var topic = simulatorSettings.Topics.FirstOrDefault(t =>
            string.Equals(t.Name, request.TopicName, StringComparison.OrdinalIgnoreCase)
        );

        var removed =
            (topic?.Subscribers.RemoveHttpSubscriber(request.EventSubscriptionName) ?? false)
            || (
                topic?.Subscribers.RemoveStorageQueueSubscriber(request.EventSubscriptionName)
                ?? false
            );

        if (removed)
        {
            logger.LogInformation(
                "Deleted runtime event subscription '{SubscriptionName}' on topic '{TopicName}'",
                request.EventSubscriptionName,
                request.TopicName
            );
        }

        return Task.FromResult(removed);
    }
}
