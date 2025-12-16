using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
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
                                  EventSchemaDetector schemaDetector,
                                  EventSchemaParserFactory parserFactory,
                                  ILogger<EventGridMiddleware> logger)
    {
        if (IsNotificationRequest(context))
        {
            await ValidateNotificationRequest(context, simulatorSettings, sasHeaderValidator, schemaDetector, parserFactory, logger);
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
                                                   EventSchemaDetector schemaDetector,
                                                   EventSchemaParserFactory parserFactory,
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

        //
        // Validate the overall body size.
        //
        const int maximumAllowedOverallMessageSizeInBytes = 1536000;
        const int maximumAllowedEventSizeInBytes = 1049600;

        if (requestBody.Length > maximumAllowedOverallMessageSizeInBytes)
        {
            logger.LogError("Payload is larger than the allowed maximum");

            await context.WriteErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Payload is larger than the allowed maximum.", null);
            return;
        }

        //
        // Detect the schema (use configured input schema or auto-detect)
        //
        var detectedSchema = topic.InputSchema ?? schemaDetector.DetectSchema(context);
        var parser = parserFactory.GetParser(detectedSchema);

        SimulatorEvent[] events;
        try
        {
            events = parser.Parse(context, requestBody);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to parse events");
            await context.WriteErrorResponse(HttpStatusCode.BadRequest, ex.Message, null);
            return;
        }

        if (events == null || events.Length == 0)
        {
            await context.WriteErrorResponse(HttpStatusCode.BadRequest, "No events found in the request body.", null);
            return;
        }

        //
        // Validate the size of each event.
        //
        foreach (var evt in events)
        {
            var eventSize = evt.Schema == EventSchema.EventGridSchema
                ? JsonConvert.SerializeObject(evt.EventGridEvent, Formatting.None).Length
                : JsonConvert.SerializeObject(evt.CloudEvent, Formatting.None).Length;

            if (eventSize > maximumAllowedEventSizeInBytes)
            {
                logger.LogError("Event is larger than the allowed maximum");

                await context.WriteErrorResponse(HttpStatusCode.RequestEntityTooLarge, "Event is larger than the allowed maximum.", null);
                return;
            }
        }

        //
        // Validate the properties of each event.
        //
        try
        {
            parser.Validate(events);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Event was not valid");

            await context.WriteErrorResponse(HttpStatusCode.BadRequest, ex.Message, null);
            return;
        }

        // Store parsed events in HttpContext for use by the controller
        context.Items["ParsedEvents"] = events;
        context.Items["DetectedSchema"] = detectedSchema;

        await _next(context);
    }

    private async Task ValidateHealthRequest(HttpContext context)
    {
        await _next(context);
    }

    private static bool IsNotificationRequest(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post ||
            !string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for CloudEvents binary mode (indicated by ce-* headers)
        if (IsCloudEventsBinaryMode(context))
        {
            return true;
        }

        if (!context.Request.Headers.Keys.Any(k => string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var contentType = context.Request.Headers["Content-Type"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        // Accept EventGrid format (application/json) or CloudEvents format (use base types for detection)
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains(Constants.CloudEventsContentTypeBase, StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains(Constants.CloudEventsBatchContentTypeBase, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCloudEventsBinaryMode(HttpContext context)
    {
        var headers = context.Request.Headers;
        return headers.ContainsKey(Constants.CeSpecVersionHeader) &&
               headers.ContainsKey(Constants.CeIdHeader) &&
               headers.ContainsKey(Constants.CeSourceHeader) &&
               headers.ContainsKey(Constants.CeTypeHeader);
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
