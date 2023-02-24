namespace AzureEventGridSimulator.Controllers;

using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Filters;
using AzureEventGridSimulator.Infrastructure.ModelBinders;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("/api/events")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
[ServiceFilter(typeof(MaxContentLengthAttribute))]
[Consumes("application/json")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [EventType(EventType.EventGridEvent)]
    public async Task<IActionResult> Post([FromFeatures] TopicSettings topic, EventGridEvent[] events)
    {
        await _mediator.Send(new SendNotificationEventsToSubscriberCommand<EventGridEvent>(events, topic));

        return Ok();
    }

    [HttpPost]
    [EventType(EventType.CloudEvent)]
    public async Task<IActionResult> Post([FromFeatures] TopicSettings topic, CloudEvent[] events)
    {
        await _mediator.Send(new SendNotificationEventsToSubscriberCommand<CloudEvent>(events, topic));

        return Ok();
    }
}
