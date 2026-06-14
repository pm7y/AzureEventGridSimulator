using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;

namespace AzureEventGridSimulator.Domain.Commands;

public enum EventSubscriptionWriteOutcome
{
    Created,
    Updated,
    TopicNotFound,
    UnsupportedDestination,
}

public record CreateOrUpdateEventSubscriptionResult(
    EventSubscriptionWriteOutcome Outcome,
    ArmEventSubscriptionResource? Resource
);

public class CreateOrUpdateEventSubscriptionCommand(
    EventSubscriptionScope scope,
    string eventSubscriptionName,
    ArmEventSubscriptionResource resource
) : IRequest<CreateOrUpdateEventSubscriptionResult>
{
    public EventSubscriptionScope Scope { get; } = scope;

    public string EventSubscriptionName { get; } = eventSubscriptionName;

    public ArmEventSubscriptionResource Resource { get; } = resource;
}
