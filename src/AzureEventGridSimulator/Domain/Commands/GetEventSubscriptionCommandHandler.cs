using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class GetEventSubscriptionCommandHandler(SimulatorSettings simulatorSettings)
    : IRequestHandler<GetEventSubscriptionCommand, ArmEventSubscriptionResource?>
{
    public Task<ArmEventSubscriptionResource?> Handle(
        GetEventSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        var topic = simulatorSettings.Topics.FirstOrDefault(t =>
            string.Equals(t.Name, request.Scope.TopicName, StringComparison.OrdinalIgnoreCase)
        );

        ISubscriberSettings? subscriber = topic?.Subscribers.HttpSubscribers.FirstOrDefault(s =>
            string.Equals(s.Name, request.EventSubscriptionName, StringComparison.OrdinalIgnoreCase)
        );

        subscriber ??= topic?.Subscribers.StorageQueueSubscribers.FirstOrDefault(s =>
            string.Equals(s.Name, request.EventSubscriptionName, StringComparison.OrdinalIgnoreCase)
        );

        return Task.FromResult(
            subscriber is null ? null : EventSubscriptionMapper.MapToArm(request.Scope, subscriber)
        );
    }
}
