using System;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Middleware
{
    public class AegEventValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public AegEventValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, ILogger logger)
        {
            var events = context.RetrieveEvents();

            foreach (var evt in events)
            {
                try
                {
                    evt.Validate();
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogError(ex, "Event was not valid.");

                    await context.Response.ErrorResponse(HttpStatusCode.BadRequest, ex.Message);
                    return;
                }
            }

            await _next(context);
        }
    }
}
