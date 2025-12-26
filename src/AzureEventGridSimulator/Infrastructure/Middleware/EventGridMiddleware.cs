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

        // Azure returns 200 for OPTIONS (CORS preflight support)
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase)
            && context.Request.Method == HttpMethods.Options
        )
        {
            var topic = simulatorSettings.Topics.FirstOrDefault(t =>
                t.Port == context.Request.Host.Port
            );
            var schemaName =
                topic?.InputSchema == EventSchema.CloudEventV1_0
                    ? "CloudEventV10"
                    : "EventGridEvent";

            context.Response.Headers.Append("Allow", "POST, OPTIONS");
            context.Response.Headers.Append("api-supported-versions", "2018-01-01");
            context.Response.Headers.Append("aeg-input-event-schema", schemaName);
            context.Response.Headers["x-ms-request-id"] = context.GetRequestId().ToString();
            context.Response.StatusCode = 200;
            return;
        }

        // Azure returns 404 for HEAD on /api/events
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase)
            && context.Request.Method == HttpMethods.Head
        )
        {
            context.Response.StatusCode = 404;
            return;
        }

        // Azure returns 405 Method Not Allowed for non-POST methods to /api/events (exact path)
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.Ordinal)
            && context.Request.Method != HttpMethods.Post
        )
        {
            context.Response.Headers.Append("Allow", "OPTIONS, POST");
            await context.WriteErrorResponse(
                HttpStatusCode.MethodNotAllowed,
                $".{context.GenerateReportSuffix()}",
                null,
                ErrorDetailCodes.MethodNotAllowed
            );
            return;
        }

        // This is the end of the line - unknown path
        // Azure returns 404 Not Found for unknown paths regardless of HTTP method
        var requestUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        await context.WriteErrorResponse(
            HttpStatusCode.NotFound,
            $"No HTTP resource was found that matches the request URI '{Uri.EscapeDataString(requestUri)}'.{context.GenerateReportSuffix()}",
            null,
            ErrorDetailCodes.ResourceNotFound
        );
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
        if (!string.IsNullOrWhiteSpace(topic.Key))
        {
            var validationResult = sasHeaderValidator.Validate(context.Request.Headers, topic.Key);
            if (!validationResult.IsValid)
            {
                // Azure returns 400 Bad Request for empty keys with InvalidSas code
                if (validationResult.FailureReason == SasValidationFailureReason.EmptyKey)
                {
                    await context.WriteErrorResponse(
                        HttpStatusCode.BadRequest,
                        $"Request must have a value for aeg-sas-key.{context.GenerateReportSuffix()}",
                        null,
                        ErrorDetailCodes.InvalidSas
                    );
                    return;
                }

                await context.WriteErrorResponse(
                    HttpStatusCode.Unauthorized,
                    GetAuthErrorMessage(
                        context,
                        validationResult.FailureReason,
                        context.Request.Host.Host
                    ),
                    null,
                    ErrorDetailCodes.Unauthorized
                );
                return;
            }
        }

        context.Request.EnableBuffering();
        var requestBody = await context.RequestBody();

        //
        // Azure returns 400 for empty body in binary mode CloudEvents
        //
        if (IsCloudEventsBinaryMode(context) && string.IsNullOrWhiteSpace(requestBody))
        {
            var errorMessage = $"Unexpected end when reading JSON.{context.GenerateReportSuffix()}";
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.BadRequest,
                errorMessage,
                null,
                ErrorDetailCodes.InputJsonInvalid
            );
            return;
        }

        //
        // Validate the overall body size.
        //
        const int maximumAllowedOverallMessageSizeInBytes = 1536000;
        const int maximumAllowedEventSizeInBytes = 1049600;

        if (requestBody.Length > maximumAllowedOverallMessageSizeInBytes)
        {
            logger.LogError("Payload is larger than the allowed maximum");

            var errorMessage =
                $"The maximum size ({maximumAllowedOverallMessageSizeInBytes}) has been exceeded.";
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
                null,
                ErrorDetailCodes.PayloadTooLarge
            );
            return;
        }

        //
        // Detect the schema (use configured input schema or auto-detect)
        //
        var detectedSchema = topic.InputSchema ?? schemaDetector.DetectSchema(context);

        //
        // Validate content-type for CloudEvents schema
        //
        if (detectedSchema == EventSchema.CloudEventV1_0)
        {
            if (IsCloudEventsBinaryMode(context))
            {
                // Binary mode with structured content type is a conflict - Azure returns 400
                if (IsValidCloudEventsContentType(contentType))
                {
                    var errorMessage =
                        "Conflicting content mode: binary mode headers with structured mode content type.";

                    RecordRejection(
                        eventHistoryService,
                        topic.Name,
                        topic.Port,
                        HttpStatusCode.BadRequest,
                        errorMessage,
                        requestBody,
                        contentType
                    );

                    await context.WriteErrorResponse(
                        HttpStatusCode.BadRequest,
                        errorMessage,
                        null,
                        ErrorDetailCodes.InputJsonInvalid
                    );
                    return;
                }

                // Binary mode: Content-Type is the data's content type
                // Azure only accepts application/json for binary mode
                if (!IsValidBinaryModeContentType(contentType))
                {
                    var errorMessage =
                        "The Content-Type header is either missing or it doesn't have a valid value. The content type header must either be application/cloudevents+json; charset=utf-8 or application/cloudevents-batch+json; charset=UTF-8."
                        + context.GenerateReportSuffix();

                    RecordRejection(
                        eventHistoryService,
                        topic.Name,
                        topic.Port,
                        HttpStatusCode.UnsupportedMediaType,
                        errorMessage,
                        requestBody,
                        contentType
                    );

                    await context.WriteErrorResponse(
                        HttpStatusCode.UnsupportedMediaType,
                        errorMessage,
                        null,
                        ErrorDetailCodes.InvalidContentType
                    );
                    return;
                }
            }
            else
            {
                // Structured/batch mode: require CloudEvents content types OR application/json
                // Azure accepts application/json and treats it as single CloudEvent (not batch)
                // It will fail with 400 if the body is an array instead of an object
                if (!IsValidCloudEventsContentType(contentType) && !IsApplicationJson(contentType))
                {
                    var errorMessage =
                        "The Content-Type header is either missing or it doesn't have a valid value. The content type header must either be application/cloudevents+json; charset=utf-8 or application/cloudevents-batch+json; charset=UTF-8."
                        + context.GenerateReportSuffix();

                    RecordRejection(
                        eventHistoryService,
                        topic.Name,
                        topic.Port,
                        HttpStatusCode.UnsupportedMediaType,
                        errorMessage,
                        requestBody,
                        contentType
                    );

                    await context.WriteErrorResponse(
                        HttpStatusCode.UnsupportedMediaType,
                        errorMessage,
                        null,
                        ErrorDetailCodes.InvalidContentType
                    );
                    return;
                }
            }
        }

        var parser = parserFactory.GetParser(detectedSchema);

        SimulatorEvent[] events;
        try
        {
            events = parser.Parse(context, requestBody);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to parse events");

            // Determine the appropriate error code based on the exception message
            var errorCode = ex.Message.Contains("header", StringComparison.OrdinalIgnoreCase)
                ? ErrorDetailCodes.InvalidCloudEventHeader
                : ErrorDetailCodes.InputJsonInvalid;

            // Add Report suffix to parser errors
            var errorMessage = $"{ex.Message}{context.GenerateReportSuffix()}";

            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.BadRequest,
                errorMessage,
                null,
                errorCode
            );
            return;
        }

        if (events == null || events.Length == 0)
        {
            var schemaName =
                detectedSchema == EventSchema.CloudEventV1_0 ? "CloudEventV10" : "EventGridEvent";
            var errorMessage =
                $"This resource is configured to receive event in '{schemaName}' schema. "
                + "The JSON received does not conform to the expected schema.";
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.BadRequest,
                errorMessage,
                null,
                ErrorDetailCodes.InputJsonInvalid
            );
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

                var errorMessage =
                    $"The maximum size ({maximumAllowedEventSizeInBytes}) has been exceeded.";
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
                    null,
                    ErrorDetailCodes.PayloadTooLarge
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
        catch (TopicAuthorizationException ex)
        {
            // Azure returns 401 when topic field is set (doesn't match endpoint)
            logger.LogError(ex, "Topic authorization failed");

            // Add Report suffix to topic authorization errors
            var errorMessage = $"{ex.Message}{context.GenerateReportSuffix()}";

            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.Unauthorized,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.Unauthorized,
                errorMessage,
                null,
                ErrorDetailCodes.Unauthorized
            );
            return;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Event was not valid");

            // Add Azure-style report suffix to validation errors if not already present
            var errorMessage = ex.Message.Contains("Report '", StringComparison.Ordinal)
                ? ex.Message
                : ex.Message + context.GenerateReportSuffix();

            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                HttpStatusCode.BadRequest,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                HttpStatusCode.BadRequest,
                errorMessage,
                null,
                ErrorDetailCodes.InputJsonInvalid
            );
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
        // Azure accepts paths in any case and with trailing slash
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        if (
            context.Request.Method != HttpMethods.Post
            || !string.Equals(path, "/api/events", StringComparison.OrdinalIgnoreCase)
        )
            return false;

        // Check for CloudEvents binary mode (indicated by ce-* headers)
        if (IsCloudEventsBinaryMode(context))
            return true;

        var contentType = context.Request.Headers.ContentType.FirstOrDefault();

        // Azure Event Grid is lenient for EventGrid schema - accepts missing/wrong content-type
        // For CloudEvents, we'll validate the content-type later and return 415 if invalid
        // Accept all POST /api/events requests and let the schema detection/validation handle it
        if (string.IsNullOrWhiteSpace(contentType))
            // Accept requests without Content-Type - EventGrid schema is lenient
            return true;

        // Accept EventGrid format (application/json) or CloudEvents format (use base types for detection)
        // Also accept text/plain and other content types - Azure is lenient for EventGrid schema
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains(
                Constants.CloudEventsContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            )
            || contentType.Contains(
                Constants.CloudEventsBatchContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            )
            || !contentType.Contains("cloudevents", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCloudEventsBinaryMode(HttpContext context)
    {
        var headers = context.Request.Headers;
        // Binary mode is detected when any ce-* header is present
        // Azure validates required headers during parsing and returns specific errors
        return headers.ContainsKey(Constants.CeSpecVersionHeader)
            || headers.ContainsKey(Constants.CeIdHeader)
            || headers.ContainsKey(Constants.CeSourceHeader)
            || headers.ContainsKey(Constants.CeTypeHeader);
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

    private static bool IsValidCloudEventsContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return contentType.Contains(
                Constants.CloudEventsContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            )
            || contentType.Contains(
                Constants.CloudEventsBatchContentTypeBase,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool IsApplicationJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        // Azure accepts application/json for CloudEvents and treats it as single event mode
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidBinaryModeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        // In binary mode, Content-Type represents the data's content type
        // Azure only accepts application/json for binary mode CloudEvents
        // Azure returns 415 for text/plain, application/octet-stream, etc.
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAuthErrorMessage(
        HttpContext context,
        SasValidationFailureReason? failureReason,
        string? hostName
    )
    {
        var endpoint = (hostName ?? "localhost").ToUpperInvariant();
        var reportSuffix = context.GenerateReportSuffix();

        return failureReason switch
        {
            SasValidationFailureReason.MissingKey =>
                $"Request must contain one of the following authorization signature: aeg-sas-token, aeg-sas-key.{reportSuffix}",
            SasValidationFailureReason.KeyMismatch =>
                $"The request authorization key is not authorized for {endpoint}. This is due to the reason: The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.{reportSuffix}",
            SasValidationFailureReason.InvalidBase64 =>
                $"The request authorization key is not authorized for {endpoint}. This is due to the reason: The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.{reportSuffix}",
            SasValidationFailureReason.TokenExpired =>
                $"The specified SAS token has expired for topic '{endpoint}'.{reportSuffix}",
            SasValidationFailureReason.SignatureMismatch =>
                $"The specified SAS token signature is invalid for topic '{endpoint}'.{reportSuffix}",
            SasValidationFailureReason.InvalidTokenFormat =>
                $"The specified SAS token format is invalid for topic '{endpoint}'.{reportSuffix}",
            SasValidationFailureReason.BearerTokenInvalid =>
                $"Unable to read access token.{reportSuffix}",
            SasValidationFailureReason.UnsupportedAuthScheme =>
                $"Request has an unsupported Authorization scheme (must be SharedAccessSignature or Bearer).{reportSuffix}",
            _ =>
                $"Request must contain one of the following authorization signature: aeg-sas-token, aeg-sas-key.{reportSuffix}",
        };
    }
}
