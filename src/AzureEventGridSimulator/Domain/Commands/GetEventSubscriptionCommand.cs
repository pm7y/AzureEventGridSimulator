using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;

namespace AzureEventGridSimulator.Domain.Commands;

public class GetEventSubscriptionCommand(EventSubscriptionScope scope, string eventSubscriptionName)
    : IRequest<ArmEventSubscriptionResource?>
{
    public EventSubscriptionScope Scope { get; } = scope;

    public string EventSubscriptionName { get; } = eventSubscriptionName;
}
