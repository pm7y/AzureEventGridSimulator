using System.Linq;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/events")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class NotificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly SimulatorSettings _simulatorSettings;

    public NotificationController(SimulatorSettings simulatorSettings,
                                  IMediator mediator)
    {
        _mediator = mediator;
        _simulatorSettings = simulatorSettings;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var topicSettingsForCurrentRequestPort = _simulatorSettings.Topics.First(t => t.Port == HttpContext.Request.Host.Port);

        // Events are parsed and validated in the middleware
        var events = (SimulatorEvent[])HttpContext.Items["ParsedEvents"];
        var detectedSchema = (EventSchema)HttpContext.Items["DetectedSchema"];

        await _mediator.Send(new SendNotificationEventsToSubscriberCommand(events, topicSettingsForCurrentRequestPort, detectedSchema));

        return Ok();
    }
}
