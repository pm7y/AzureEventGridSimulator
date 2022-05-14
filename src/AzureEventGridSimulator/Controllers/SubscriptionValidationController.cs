using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/validate")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class SubscriptionValidationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly SimulatorSettings _simulatorSettings;

    public SubscriptionValidationController(SimulatorSettings simulatorSettings,
                                            IMediator mediator)
    {
        _simulatorSettings = simulatorSettings;
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        var topicSettingsForCurrentRequestPort = _simulatorSettings.Topics.First(t => t.Port == HttpContext.Request.Host.Port);
        var isValid = await _mediator.Send(new ValidateSubscriptionCommand(topicSettingsForCurrentRequestPort, id));

        if (!isValid)
        {
            return BadRequest(new ErrorMessage(HttpStatusCode.BadRequest, "The validation code was not correct.", null));
        }

        return Ok("Webhook successfully validated as a subscription endpoint");
    }
}
