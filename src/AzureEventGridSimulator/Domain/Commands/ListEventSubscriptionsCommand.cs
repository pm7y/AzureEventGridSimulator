using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;

namespace AzureEventGridSimulator.Domain.Commands;

public class ListEventSubscriptionsCommand(EventSubscriptionScope scope)
    : IRequest<ArmEventSubscriptionList?>
{
    public EventSubscriptionScope Scope { get; } = scope;
}
