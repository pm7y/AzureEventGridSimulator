using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class NotificationController : SimulatorController
    {
        private readonly IMediator _mediator;

        public NotificationController(SimulatorSettings simulatorSettings,
                                      IMediator mediator) : base(simulatorSettings)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            await _mediator.Send(new SendNotificationEventsToSubscriberCommand(Events, Topic));

            return Ok();
        }
    }
}
