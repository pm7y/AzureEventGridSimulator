using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Middleware
{
    public class AegSasHeaderValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public AegSasHeaderValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, SasKeyValidator aegSasHeaderValidator)
        {
            var requestPort = context.Connection.LocalPort;
            var topic = simulatorSettings.Topics.First(t => t.Port == requestPort);

            if (!string.IsNullOrWhiteSpace(topic.Key) &&
                !aegSasHeaderValidator.IsValid(context.Request.Headers, topic.Key))
            {
                await context.Response.ErrorResponse(HttpStatusCode.Unauthorized, "The request did not contain a valid aeg-sas-key or aeg-sas-token.");
                return;
            }

            await _next(context);
        }
    }
}
