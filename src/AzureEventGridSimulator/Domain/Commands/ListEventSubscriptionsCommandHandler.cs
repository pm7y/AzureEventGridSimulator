using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class ListEventSubscriptionsCommandHandler(SimulatorSettings simulatorSettings)
    : IRequestHandler<ListEventSubscriptionsCommand, ArmEventSubscriptionList?>
{
    public Task<ArmEventSubscriptionList?> Handle(
        ListEventSubscriptionsCommand request,
        CancellationToken cancellationToken
    )
    {
        var topic = simulatorSettings.Topics.FirstOrDefault(t =>
            string.Equals(t.Name, request.Scope.TopicName, StringComparison.OrdinalIgnoreCase)
        );

        if (topic is null)
        {
            return Task.FromResult<ArmEventSubscriptionList?>(null);
        }

        var list = new ArmEventSubscriptionList
        {
            Value = topic
                .Subscribers.HttpSubscribers.Select(s =>
                    EventSubscriptionMapper.MapToArm(request.Scope, s)
                )
                .ToList(),
        };

        return Task.FromResult<ArmEventSubscriptionList?>(list);
    }
}
