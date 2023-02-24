namespace AzureEventGridSimulator.Controllers;

using System;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands.Http;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[Route("/validate")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class SubscriptionValidationController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionValidationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        var topicSettingsForCurrentRequestPort = HttpContext.GetTopic();
        var isValid = await _mediator.Send(new ValidateHttpSubscriptionCommand(topicSettingsForCurrentRequestPort, id));

        if (!isValid)
        {
            return BadRequest(new ErrorMessage(HttpStatusCode.BadRequest, "The validation code was not correct.", null));
        }

        return Ok("Webhook successfully validated as a subscription endpoint");
    }
}
