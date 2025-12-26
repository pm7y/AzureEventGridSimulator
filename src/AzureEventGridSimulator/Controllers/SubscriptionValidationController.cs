using System.Net;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
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
        var topicSettingsForCurrentRequestPort = simulatorSettings.Topics.First(t =>
            t.Port == HttpContext.Request.Host.Port
        );
        var isValid = await mediator.Send(
            new ValidateSubscriptionCommand(topicSettingsForCurrentRequestPort, id)
        );

        if (!isValid)
            return BadRequest(
                new ErrorMessage(
                    HttpStatusCode.BadRequest,
                    "The validation code was not correct.",
                    null,
                    ErrorDetailCodes.InputJsonInvalid
                )
            );

        return Ok("Webhook successfully validated as a subscription endpoint");
    }
}
