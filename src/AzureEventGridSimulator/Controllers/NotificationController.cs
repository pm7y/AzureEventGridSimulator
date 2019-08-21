using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly IMediator _mediator;

        public NotificationController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            await _mediator.Send(new SendNotificationEventsToSubscriberCommand(HttpContext.RetrieveEvents(),
                                                                               HttpContext.RetrieveTopicSettings()));

            return Ok();
        }
    }
}
