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
        private const int MaximumAllowedOverallMessageSizeInBytes = 1536000;
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

            logger.LogDebug("Message is {Bytes} in length.", requestBody.Length);

            if (requestBodyLength > MaximumAllowedOverallMessageSizeInBytes)
            {
                logger.LogError("Payload is larger than the allowed maximum.");

                await context.Response.ErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Payload is larger than the allowed maximum.");
                return;
            }

            var events = context.RetrieveEvents();

            foreach (var evt in events)
            {
                var eventJson = JsonConvert.SerializeObject(evt, Formatting.Indented);
                var eventJsonLength = eventJson.Length;

                logger.LogDebug("Event is {Bytes} in length.", eventJsonLength);

                if (eventJsonLength > MaximumAllowedEventGridEventSizeInBytes)
                {
                    logger.LogError("Event is larger than the allowed maximum.");

                    await context.Response.ErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Event is larger than the allowed maximum.");
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

                    await context.Response.ErrorResponse(HttpStatusCode.BadRequest, ex.Message);
                    return;
                }
            }

            await _next(context);
        }
    }
}
