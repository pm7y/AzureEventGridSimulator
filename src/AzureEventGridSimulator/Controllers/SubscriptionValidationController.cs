﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/validate")]
    [ApiController]
    public class SubscriptionValidationController : ControllerBase
    {
        private readonly SimulatorSettings _simulatorSettings;
        private readonly IMediator _mediator;

        public SubscriptionValidationController(SimulatorSettings simulatorSettings,
                                                IMediator mediator)
        {
            _simulatorSettings = simulatorSettings;
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            var topicSettingsForCurrentRequestPort = _simulatorSettings.Topics.First(t => t.Port == HttpContext.Connection.LocalPort);
            var isValid = await _mediator.Send(new ValidateSubscriptionCommand(topicSettingsForCurrentRequestPort, id));

            if (!isValid)
            {
                return BadRequest(new ErrorMessage(HttpStatusCode.BadRequest, "The validation code was not correct.", null));
            }

            return Ok("Webhook successfully validated as a subscription endpoint");
        }
    }
}
