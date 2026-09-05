using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Mediator;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

/// <summary>
///     The ARM control-plane facade. It speaks the same HTTP that the Azure.ResourceManager.EventGrid
///     client emits for topic-scoped event subscriptions, so that client can create, get, list and
///     delete subscriptions against the simulator at runtime simply by being repointed at the
///     management port. WebHook and StorageQueue destinations are supported.
/// </summary>
[ApiController]
[ApiVersion(Constants.SupportedManagementApiVersion)]
[Route(
    "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"
        + "/providers/Microsoft.EventGrid/topics/{topicName}/eventSubscriptions"
)]
public class EventSubscriptionsManagementController(IMediator mediator) : ControllerBase
{
    [HttpPut("{eventSubscriptionName}")]
    public async Task<IActionResult> CreateOrUpdate(
        string subscriptionId,
        string resourceGroupName,
        string topicName,
        string eventSubscriptionName,
        [FromBody] ArmEventSubscriptionResource resource
    )
    {
        var scope = new EventSubscriptionScope(subscriptionId, resourceGroupName, topicName);

        var result = await mediator.Send(
            new CreateOrUpdateEventSubscriptionCommand(scope, eventSubscriptionName, resource)
        );

        return result.Outcome switch
        {
            EventSubscriptionWriteOutcome.Created => Created(
                EventSubscriptionMapper.BuildResourceId(scope, eventSubscriptionName),
                result.Resource
            ),
            EventSubscriptionWriteOutcome.Updated => Ok(result.Resource),
            EventSubscriptionWriteOutcome.TopicNotFound => NotFound(),
            _ => BadRequest(),
        };
    }

    [HttpGet("{eventSubscriptionName}")]
    public async Task<IActionResult> Get(
        string subscriptionId,
        string resourceGroupName,
        string topicName,
        string eventSubscriptionName
    )
    {
        var resource = await mediator.Send(
            new GetEventSubscriptionCommand(
                new EventSubscriptionScope(subscriptionId, resourceGroupName, topicName),
                eventSubscriptionName
            )
        );

        return resource is null ? NotFound() : Ok(resource);
    }

    [HttpDelete("{eventSubscriptionName}")]
    public async Task<IActionResult> Delete(string topicName, string eventSubscriptionName)
    {
        await mediator.Send(new DeleteEventSubscriptionCommand(topicName, eventSubscriptionName));

        // Delete is idempotent: Azure returns success whether or not the subscription existed.
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> List(
        string subscriptionId,
        string resourceGroupName,
        string topicName
    )
    {
        var list = await mediator.Send(
            new ListEventSubscriptionsCommand(
                new EventSubscriptionScope(subscriptionId, resourceGroupName, topicName)
            )
        );

        return list is null ? NotFound() : Ok(list);
    }
}
