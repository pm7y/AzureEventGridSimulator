using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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

        var eventsFromCurrentRequestBody = JsonConvert.DeserializeObject<EventGridEvent[]>(await HttpContext.RequestBody());

        await _mediator.Send(new SendNotificationEventsToSubscriberCommand(eventsFromCurrentRequestBody, topicSettingsForCurrentRequestPort));

        return Ok();
    }


    [Route("cloudevent")]
    [HttpPost]
    public async Task<IActionResult> PostCloudEvent()
    {

        var topicSettingsForCurrentRequestPort = _simulatorSettings.Topics.First(t => t.Port == HttpContext.Request.Host.Port);

        var eventsFromCurrentRequestBody = JsonConvert.DeserializeObject<CloudEvent[]>(await HttpContext.RequestBody());

        await _mediator.Send(new SendNotificationCloudEventsToSubscriberCommand(eventsFromCurrentRequestBody, topicSettingsForCurrentRequestPort));

        return Ok();
    }
}
