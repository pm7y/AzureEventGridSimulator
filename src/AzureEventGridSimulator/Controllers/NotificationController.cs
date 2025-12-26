using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
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

        // Events are parsed and validated by EventParsingMiddleware
        if (HttpContext.Items["ParsedEvents"] is not SimulatorEvent[] events)
            throw new InvalidOperationException(
                "ParsedEvents not found in HttpContext. Ensure EventParsingMiddleware is configured."
            );

        if (HttpContext.Items["DetectedSchema"] is not EventSchema detectedSchema)
            throw new InvalidOperationException(
                "DetectedSchema not found in HttpContext. Ensure EventParsingMiddleware is configured."
            );

        await mediator.Send(
            new SendNotificationEventsToSubscriberCommand(
                events,
                topicSettingsForCurrentRequestPort,
                detectedSchema
            )
        );

        // Azure returns x-ms-request-id and api-supported-versions headers on success responses
        HttpContext.Response.Headers["x-ms-request-id"] = HttpContext.GetRequestId().ToString();
        HttpContext.Response.Headers["api-supported-versions"] = Constants.SupportedApiVersion;

        return Ok();
    }
}
