using System.Net;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/validate")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class SubscriptionValidationController(
    SimulatorSettings simulatorSettings,
    IMediator mediator
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        // No enabled topic on this port (e.g. a dedicated dashboard port, or a disabled
        // topic's port) is an unknown resource, the same as any other unmatched request.
        var topicSettingsForCurrentRequestPort = simulatorSettings.Topics.FirstOrDefault(t =>
            !t.Disabled && t.Port == HttpContext.Request.Host.Port
        );
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
