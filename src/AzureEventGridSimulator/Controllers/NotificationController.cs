using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using Microsoft.AspNetCore.Mvc;
using static AzureEventGridSimulator.Infrastructure.Extensions.HttpContextExtensions;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/events")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class NotificationController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // EventGridMiddleware routes the request to its topic, authenticates it and parses and
        // validates the events before this action runs
        var publish = HttpContext.GetValidatedPublish();

        await mediator.Send(
            new SendNotificationEventsToSubscriberCommand(
                publish.Events,
                publish.Topic,
                publish.Schema
            )
        );

        // Azure returns x-ms-request-id and api-supported-versions headers on success responses
        HttpContext.Response.Headers[RequestIdKey] = HttpContext.GetRequestId().ToString();
        HttpContext.Response.Headers["api-supported-versions"] = Constants.SupportedApiVersion;

        return Ok();
    }
}
