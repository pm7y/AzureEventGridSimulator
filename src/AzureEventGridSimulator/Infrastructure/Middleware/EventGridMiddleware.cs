#nullable enable

using System.Net;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Middleware;

public class EventGridMiddleware(RequestDelegate next)
{
    // ReSharper disable once UnusedMember.Global
    public async Task InvokeAsync(
        HttpContext context,
        SimulatorSettings simulatorSettings,
        SasKeyValidator sasHeaderValidator,
        EventSchemaDetector schemaDetector,
        EventSchemaParserFactory parserFactory,
        IEventHistoryService eventHistoryService,
        ILogger<EventGridMiddleware> logger
    )
    {
        if (IsNotificationRequest(context))
        {
            await ValidateNotificationRequest(
                context,
                simulatorSettings,
                sasHeaderValidator,
                schemaDetector,
                parserFactory,
                eventHistoryService,
                logger
            );
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

        if (IsDashboardRequest(context))
        {
            await next(context);
            return;
        }

        // Ignore favicon requests - browsers request this automatically
        if (context.Request.Path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 404;
            return;
        }

        // This is the end of the line
        // Only record rejections for POST requests (events are always sent via POST)
        // GET requests for unknown paths (like source maps) should just return 404
        if (context.Request.Method != HttpMethods.Post)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var errorMessage = "Request not supported.";
        var topic = simulatorSettings.Topics.FirstOrDefault(t =>
            t.Port == context.Request.Host.Port
        );
        if (topic != null)
        {
            context.Request.EnableBuffering();
            var rawBody = await TryReadBody(context);
            var contentType = context.Request.Headers.ContentType.FirstOrDefault();
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                rawBody,
                contentType
            );
        }

        await context.WriteErrorResponse(HttpStatusCode.BadRequest, errorMessage, null);
    }

    private static async Task<string?> TryReadBody(HttpContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDashboardRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments(
            "/dashboard",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private async Task ValidateSubscriptionValidationRequest(HttpContext context)
    {
        var id = context.Request.Query["id"];

        if (string.IsNullOrWhiteSpace(id))
        {
            await context.WriteErrorResponse(
                HttpStatusCode.BadRequest,
                "The request did not contain a validation code.",
                null
            );
            return;
        }

        await next(context);
    }

    private async Task ValidateNotificationRequest(
        HttpContext context,
        SimulatorSettings simulatorSettings,
        SasKeyValidator sasHeaderValidator,
        EventSchemaDetector schemaDetector,
        EventSchemaParserFactory parserFactory,
        IEventHistoryService eventHistoryService,
        ILogger logger
    )
    {
        var topic = simulatorSettings.Topics.First(t => t.Port == context.Request.Host.Port);
        var contentType = context.Request.Headers.ContentType.FirstOrDefault();

        //
        // Validate the key/ token supplied in the header.
        //
        if (
            !string.IsNullOrWhiteSpace(topic.Key)
            && !sasHeaderValidator.IsValid(context.Request.Headers, topic.Key)
        )
        {
            await context.WriteErrorResponse(
                HttpStatusCode.Unauthorized,
                "The request did not contain a valid aeg-sas-key or aeg-sas-token.",
                null
            );
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

            var errorMessage = "Payload is larger than the allowed maximum.";
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.RequestEntityTooLarge,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.RequestEntityTooLarge,
                errorMessage,
                null
            );
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

            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                ex.Message,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(HttpStatusCode.BadRequest, ex.Message, null);
            return;
        }

        if (events == null || events.Length == 0)
        {
            var errorMessage = "No events found in the request body.";
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(HttpStatusCode.BadRequest, errorMessage, null);
            return;
        }

        //
        // Validate the size of each event.
        //
        foreach (var evt in events)
        {
            var eventSize =
                evt.Schema == EventSchema.EventGridSchema
                    ? JsonSerializer.Serialize(evt.EventGridEvent).Length
                    : JsonSerializer.Serialize(evt.CloudEvent).Length;

            if (eventSize > maximumAllowedEventSizeInBytes)
            {
                logger.LogError("Event is larger than the allowed maximum");

                var errorMessage = "Event is larger than the allowed maximum.";
                RecordRejection(
                    eventHistoryService,
                    topic.Name,
                    topic.Port,
                    HttpStatusCode.RequestEntityTooLarge,
                    errorMessage,
                    requestBody,
                    contentType
                );

                await context.WriteErrorResponse(
                    HttpStatusCode.RequestEntityTooLarge,
                    errorMessage,
                    null
                );
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

            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                ex.Message,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(HttpStatusCode.BadRequest, ex.Message, null);
            return;
        }

        // Store parsed events in HttpContext for use by the controller
        context.Items["ParsedEvents"] = events;
        context.Items["DetectedSchema"] = detectedSchema;

        await next(context);
    }

    private static void RecordRejection(
        IEventHistoryService eventHistoryService,
        string topicName,
        int topicPort,
        HttpStatusCode statusCode,
        string errorMessage,
        string? rawBody,
        string? contentType
    )
    {
        var rejection = RejectedEventRecord.Create(
            topicName,
            topicPort,
            statusCode,
            errorMessage,
            rawBody,
            contentType
        );
        eventHistoryService.RecordEventRejected(rejection);
    }

    private async Task ValidateHealthRequest(HttpContext context)
    {
        await next(context);
    }

    private static bool IsNotificationRequest(HttpContext context)
    {
        if (
            context.Request.Method != HttpMethods.Post
            || !string.Equals(
                context.Request.Path,
                "/api/events",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        // Check for CloudEvents binary mode (indicated by ce-* headers)
        if (IsCloudEventsBinaryMode(context))
        {
            return true;
        }

        if (
            !context.Request.Headers.Keys.Any(k =>
                string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return false;
        }

        var contentType = context.Request.Headers["Content-Type"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        // Accept EventGrid format (application/json) or CloudEvents format (use base types for detection)
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains(
                Constants.CloudEventsContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            )
            || contentType.Contains(
                Constants.CloudEventsBatchContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool IsCloudEventsBinaryMode(HttpContext context)
    {
        var headers = context.Request.Headers;
        return headers.ContainsKey(Constants.CeSpecVersionHeader)
            && headers.ContainsKey(Constants.CeIdHeader)
            && headers.ContainsKey(Constants.CeSourceHeader)
            && headers.ContainsKey(Constants.CeTypeHeader);
    }

    private static bool IsValidationRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Get
            && string.Equals(context.Request.Path, "/validate", StringComparison.OrdinalIgnoreCase)
            && context.Request.Query.Keys.Any(k =>
                string.Equals(k, "id", StringComparison.OrdinalIgnoreCase)
            )
            && Guid.TryParse(context.Request.Query["id"], out _);
    }

    private static bool IsHealthRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Get
            && string.Equals(
                context.Request.Path,
                "/api/health",
                StringComparison.OrdinalIgnoreCase
            );
    }
}
