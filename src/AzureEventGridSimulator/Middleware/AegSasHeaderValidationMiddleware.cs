using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, IAegSasHeaderValidator aegSasHeaderValidator)
        {
            var requestPort = context.Connection.LocalPort;
            var topic = simulatorSettings.Topics.First(t => t.HttpsPort == requestPort);

            if (!aegSasHeaderValidator.IsValid(context.Request.Headers, topic.Key))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            await _next(context);
        }
    }
}
