using System;
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
    public class SubscriptionValidationController : SimulatorController
    {
        private readonly IMediator _mediator;

        public SubscriptionValidationController(SimulatorSettings simulatorSettings,
                                                IMediator mediator) : base(simulatorSettings)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            var isValid = await _mediator.Send(new ValidateSubscriptionCommand(Topic, id));

            if (!isValid)
            {
                return BadRequest(new ErrorMessage(HttpStatusCode.BadRequest, "The validation code was not correct."));
            }

            return Ok("Webhook successfully validated as a subscription endpoint");
        }
    }
}
