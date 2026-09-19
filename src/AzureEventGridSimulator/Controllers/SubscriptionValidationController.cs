using System.Net;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/validate")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class SubscriptionValidationController(RequestRouter requestRouter, IMediator mediator)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        // No enabled topic on this port (e.g. a dedicated dashboard port, or a disabled
        // topic's port) is an unknown resource, the same as any other unmatched request.
        var topicSettingsForCurrentRequestPort = requestRouter.ResolveTopic(HttpContext);
        if (topicSettingsForCurrentRequestPort is null)
        {
            await HttpContext.WriteResourceNotFoundResponse();
            return new EmptyResult();
        }

        var isValid = await mediator.Send(
            new ValidateSubscriptionCommand(topicSettingsForCurrentRequestPort, id)
        );

        if (!isValid)
        {
            return BadRequest(
                new ErrorMessage(
                    HttpStatusCode.BadRequest,
                    "The validation code was not correct.",
                    null,
                    ErrorDetailCodes.InputJsonInvalid
                )
            );
        }

        return Ok("Webhook successfully validated as a subscription endpoint");
    }
}
