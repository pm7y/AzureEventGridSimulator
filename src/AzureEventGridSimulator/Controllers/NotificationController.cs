using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/events")]
[ApiVersion(Constants.SupportedApiVersion)]
[ApiController]
public class NotificationController(SimulatorSettings simulatorSettings, IMediator mediator)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var topicSettingsForCurrentRequestPort = simulatorSettings.Topics.First(t =>
            t.Port == HttpContext.Request.Host.Port
        );

        // Events are parsed and validated in the middleware
        var events = (SimulatorEvent[])HttpContext.Items["ParsedEvents"];
        var detectedSchema = (EventSchema)HttpContext.Items["DetectedSchema"];

        await mediator.Send(
            new SendNotificationEventsToSubscriberCommand(
                events,
                topicSettingsForCurrentRequestPort,
                detectedSchema
            )
        );

        return Ok();
    }
}
