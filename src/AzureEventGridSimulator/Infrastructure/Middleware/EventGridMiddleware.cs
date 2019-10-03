using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Middleware
{
    public class EventGridMiddleware
    {
        private readonly RequestDelegate _next;

        public EventGridMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task InvokeAsync(HttpContext context,
                                      SimulatorSettings simulatorSettings,
                                      SasKeyValidator sasHeaderValidator,
                                      ILogger logger)
        {
            if (IsNotificationRequest(context))
            {
                await ValidateNotificationRequest(context, simulatorSettings, sasHeaderValidator, logger);
                return;
            }

            if (IsValidationRequest(context))
            {
                await ValidateSubscriptionValidationRequest(context);
                return;
            }

            // This is the end of the line.
            await context.Response.ErrorResponse(HttpStatusCode.BadRequest, "Request not supported.");
        }

        private async Task ValidateSubscriptionValidationRequest(HttpContext context)
        {
            var id = context.Request.Query["id"];

            if (string.IsNullOrWhiteSpace(id))
            {
                await context.Response.ErrorResponse(HttpStatusCode.BadRequest, "The request did not contain a validation code.");
                return;
            }

            await _next(context);
        }

        private async Task ValidateNotificationRequest(HttpContext context,
                                                      SimulatorSettings simulatorSettings,
                                                      SasKeyValidator sasHeaderValidator,
                                                      ILogger logger)
        {
            var topic = simulatorSettings.Topics.First(t => t.Port == context.Connection.LocalPort);

            //
            // Validate the key/ token supplied in the header.
            //
            if (!string.IsNullOrWhiteSpace(topic.Key) &&
                !sasHeaderValidator.IsValid(context.Request.Headers, topic.Key))
            {
                await context.Response.ErrorResponse(HttpStatusCode.Unauthorized, "The request did not contain a valid aeg-sas-key or aeg-sas-token.");
                return;
            }

            context.Request.EnableBuffering();
            var requestBody = await context.RequestBody();
            var events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);

            //
            // Validate the overall body size and the size of each event.
            //
            const int maximumAllowedOverallMessageSizeInBytes = 1536000;
            const int maximumAllowedEventGridEventSizeInBytes = 66560;

            logger.LogDebug("Message is {Bytes} in length.", requestBody.Length);

            if (requestBody.Length > maximumAllowedOverallMessageSizeInBytes)
            {
                logger.LogError("Payload is larger than the allowed maximum.");

                await context.Response.ErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Payload is larger than the allowed maximum.");
                return;
            }

            foreach (var evt in events)
            {
                var eventSize = JsonConvert.SerializeObject(evt, Formatting.None).Length;

                logger.LogDebug("Event is {Bytes} in length.", eventSize);

                if (eventSize > maximumAllowedEventGridEventSizeInBytes)
                {
                    logger.LogError("Event is larger than the allowed maximum.");

                    await context.Response.ErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Event is larger than the allowed maximum.");
                    return;
                }
            }


            //
            // Validate the properties of each event.
            //
            foreach (var eventGridEvent in events)
            {
                try
                {
                    eventGridEvent.Validate();
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

        private bool IsNotificationRequest(HttpContext context)
        {
            return context.Request.Headers.Keys.Any(k => string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) &&
                   context.Request.Headers["Content-Type"].Any(v => string.Equals(v, "application/json")) &&
                   context.Request.Method == HttpMethods.Post &&
                   string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsValidationRequest(HttpContext context)
        {
            return context.Request.Method == HttpMethods.Get &&
                   string.Equals(context.Request.Path, "/validate", StringComparison.OrdinalIgnoreCase) &&
                   context.Request.Query.Keys.Any(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)) &&
                   Guid.TryParse(context.Request.Query["id"], out _);
        }
    }
}
