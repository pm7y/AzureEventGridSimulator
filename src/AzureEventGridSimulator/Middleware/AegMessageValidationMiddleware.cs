using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Controllers;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public class AegMessageValidationMiddleware
    {
        private const int MaximumAllowedOverallMessageSizeInBytes = 1 * 1024 * 1024;
        private const int MaximumAllowedEventGridEventSizeInBytes = 66560;

        private readonly RequestDelegate _next;

        public AegMessageValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, ILogger logger)
        { 
            var requestBody = context.RetrieveRequestBodyJson();
            var requestBodyLength = requestBody.Length;

            if (requestBodyLength > MaximumAllowedOverallMessageSizeInBytes)
            {
                logger.LogError("Payload is larger than the allowed maximum of 1Mb.");

                var error = new ErrorMessage(HttpStatusCode.BadRequest, "Payload is larger than the allowed maximum of 1Mb.");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                context.Response.Headers.Add("Content-type", "application/json");
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            var events = context.RetrieveEvents();

            foreach (var evt in events)
            {
                var eventJson = JsonConvert.SerializeObject(evt, Formatting.Indented);
                var eventJsonLength = eventJson.Length;

                if (eventJsonLength > MaximumAllowedEventGridEventSizeInBytes)
                {
                    logger.LogError("Event is larger than the allowed maximum of 64Kb.");

                    var error = new ErrorMessage(HttpStatusCode.BadRequest, "Event is larger than the allowed maximum of 64Kb.");
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                    context.Response.Headers.Add("Content-type", "application/json");
                    context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                    return;
                }
            }

            foreach (var evt in events)
            {
                try
                {
                    evt.Validate();
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogError(ex, "Event was not valid.");

                    var error = new ErrorMessage(HttpStatusCode.BadRequest, ex.Message);
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                    context.Response.Headers.Add("Content-type", "application/json");
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }

            await _next(context);
        }
    }
}
