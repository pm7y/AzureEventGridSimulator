using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private const string SUPPORTED_API_VERSION = "2018-01-01";

        private readonly IMediator _mediator;
        private readonly SimulatorSettings _simulatorSettings;

        public NotificationController(SimulatorSettings simulatorSettings,
                                      IMediator mediator)
        {
            _mediator = mediator;
            _simulatorSettings = simulatorSettings;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromQuery(Name = "api-version")] string apiVersion)
        {
            if (!string.IsNullOrWhiteSpace(apiVersion) && !string.Equals(SUPPORTED_API_VERSION, apiVersion))
            {
                return BadRequest(new ErrorMessage(
                                                   HttpStatusCode.BadRequest,
                                                   $"The HTTP resource that matches the request URI '{HttpContext.Request.GetDisplayUrl()}' does not support the API version '{apiVersion}'.",
                                                   "UnsupportedApiVersion"));
            }

            var topicSettingsForCurrentRequestPort = _simulatorSettings.Topics.First(t => t.Port == HttpContext.Connection.LocalPort);
            var eventsFromCurrentRequestBody = JsonConvert.DeserializeObject<EventGridEvent[]>(HttpContext.RequestBody().Result);

            await _mediator.Send(new SendNotificationEventsToSubscriberCommand(eventsFromCurrentRequestBody, topicSettingsForCurrentRequestPort));

            Response.Headers.Add("api-supported-versions", SUPPORTED_API_VERSION);
            return Ok();
        }
    }
}
