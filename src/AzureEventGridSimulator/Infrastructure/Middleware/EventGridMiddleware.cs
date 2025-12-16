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

namespace AzureEventGridSimulator.Infrastructure.Middleware;

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
                                  ILogger<EventGridMiddleware> logger)
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

        if (IsHealthRequest(context))
        {
            await ValidateHealthRequest(context);
            return;
        }

        // This is the end of the line.
        await context.WriteErrorResponse(HttpStatusCode.BadRequest, "Request not supported.", null);
    }

    private async Task ValidateSubscriptionValidationRequest(HttpContext context)
    {
        var id = context.Request.Query["id"];

        if (string.IsNullOrWhiteSpace(id))
        {
            await context.WriteErrorResponse(HttpStatusCode.BadRequest, "The request did not contain a validation code.", null);
            return;
        }

        await _next(context);
    }

    private async Task ValidateNotificationRequest(HttpContext context,
                                                   SimulatorSettings simulatorSettings,
                                                   SasKeyValidator sasHeaderValidator,
                                                   ILogger logger)
    {
        var topic = simulatorSettings.Topics.First(t => t.Port == context.Request.Host.Port);

        //
        // Validate the key/ token supplied in the header.
        //
        if (!string.IsNullOrWhiteSpace(topic.Key) &&
            !sasHeaderValidator.IsValid(context.Request.Headers, topic.Key))
        {
            await context.WriteErrorResponse(HttpStatusCode.Unauthorized, "The request did not contain a valid aeg-sas-key or aeg-sas-token.", null);
            return;
        }

        context.Request.EnableBuffering();
        var requestBody = await context.RequestBody();
        var events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);

        //
        // Validate the overall body size and the size of each event.
        //
        const int maximumAllowedOverallMessageSizeInBytes = 1536000;
        const int maximumAllowedEventGridEventSizeInBytes = 1049600;

        if (requestBody.Length > maximumAllowedOverallMessageSizeInBytes)
        {
            logger.LogError("Payload is larger than the allowed maximum");

            await context.WriteErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Payload is larger than the allowed maximum.", null);
            return;
        }

        if (events != null)
        {
            foreach (var evt in events)
            {
                var eventSize = JsonConvert.SerializeObject(evt, Formatting.None).Length;

                if (eventSize <= maximumAllowedEventGridEventSizeInBytes)
                {
                    continue;
                }

                logger.LogError("Event is larger than the allowed maximum");

                await context.WriteErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Event is larger than the allowed maximum.", null);
                return;
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
                    logger.LogError(ex, "Event was not valid");

                    await context.WriteErrorResponse(HttpStatusCode.BadRequest, ex.Message, null);
                    return;
                }
            }
        }

        await _next(context);
    }

    private async Task ValidateHealthRequest(HttpContext context)
    {
        await _next(context);
    }

    private static bool IsNotificationRequest(HttpContext context)
    {
        return context.Request.Headers.Keys.Any(k => string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) &&
               context.Request.Headers["Content-Type"].Any(v => !string.IsNullOrWhiteSpace(v) && v.Contains("application/json", StringComparison.OrdinalIgnoreCase)) &&
               context.Request.Method == HttpMethods.Post &&
               string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidationRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Get &&
               string.Equals(context.Request.Path, "/validate", StringComparison.OrdinalIgnoreCase) &&
               context.Request.Query.Keys.Any(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)) &&
               Guid.TryParse(context.Request.Query["id"], out _);
    }

    private static bool IsHealthRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Get &&
            string.Equals(context.Request.Path, "/api/health", StringComparison.OrdinalIgnoreCase);
    }
}
