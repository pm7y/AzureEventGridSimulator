using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Middleware;

public class EventGridMiddleware(RequestDelegate next)
{
    // ReSharper disable once UnusedMember.Global
    public async Task InvokeAsync(
        HttpContext context,
        SimulatorSettings simulatorSettings,
        RequestRouter requestRouter,
        SasKeyValidator sasKeyValidator,
        EventValidationOrchestrator validationOrchestrator,
        IEventHistoryService eventHistoryService,
        ILogger<EventGridMiddleware> logger
    )
    {
        // Route the request to determine its type
        var route = requestRouter.RouteRequest(context);

        switch (route.Type)
        {
            case RequestType.Notification:
                await HandleNotificationRequest(
                    context,
                    route.Topic!,
                    sasKeyValidator,
                    validationOrchestrator,
                    eventHistoryService,
                    logger
                );
                return;

            case RequestType.SubscriptionValidation:
                await HandleValidationRequest(context);
                return;

            case RequestType.Health:
                await next(context);
                return;

            case RequestType.Dashboard:
                await next(context);
                return;

            case RequestType.OptionsPreFlight:
                await HandleOptionsRequest(context, route.Topic);
                return;

            case RequestType.HeadApiEvents:
                context.Response.StatusCode = 404;
                return;

            case RequestType.FaviconIgnore:
                context.Response.StatusCode = 404;
                return;

            case RequestType.MethodNotAllowed:
                context.Response.Headers.Append("Allow", "OPTIONS, POST");
                await context.WriteErrorResponse(
                    HttpStatusCode.MethodNotAllowed,
                    $".{context.GenerateReportSuffix()}",
                    null,
                    ErrorDetailCodes.MethodNotAllowed
                );
                return;

            case RequestType.NotFound:
                await context.WriteErrorResponse(
                    HttpStatusCode.NotFound,
                    $"No HTTP resource was found that matches the request URI '{Uri.EscapeDataString($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}")}'.{context.GenerateReportSuffix()}",
                    null,
                    ErrorDetailCodes.ResourceNotFound
                );
                return;
        }
    }

    private async Task HandleNotificationRequest(
        HttpContext context,
        TopicSettings topic,
        SasKeyValidator sasKeyValidator,
        EventValidationOrchestrator validationOrchestrator,
        IEventHistoryService eventHistoryService,
        ILogger logger
    )
    {
        // 1. Validate the SAS key/token if configured
        if (!string.IsNullOrWhiteSpace(topic.Key))
        {
            var validationResult = sasKeyValidator.Validate(context.Request.Headers, topic.Key);
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

        // 2. Buffer and read request body
        context.Request.EnableBuffering();
        var requestBody = await context.RequestBody();

        // 3. Validate events through the orchestrator
        var validationResult2 = await validationOrchestrator.ValidateEvents(
            context,
            topic,
            requestBody
        );

        if (!validationResult2.IsValid)
        {
            // Add Report suffix to error message if not already present
            // Azure does not add Report suffix for 413 (RequestEntityTooLarge) errors
            var errorMessage =
                validationResult2.ErrorMessage!.Contains("Report '", StringComparison.Ordinal)
                    ? validationResult2.ErrorMessage
                : validationResult2.StatusCode == HttpStatusCode.RequestEntityTooLarge
                    ? validationResult2.ErrorMessage
                : validationResult2.ErrorMessage + context.GenerateReportSuffix();

            // Record the rejection in event history
            var contentType = context.Request.Headers.ContentType.FirstOrDefault();
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                validationResult2.StatusCode!.Value,
                errorMessage,
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                validationResult2.StatusCode!.Value,
                errorMessage,
                null,
                validationResult2.ErrorCode
            );
            return;
        }

        // 4. Store parsed events in HttpContext for use by the controller
        context.Items["ParsedEvents"] = validationResult2.Events;
        context.Items["DetectedSchema"] = validationResult2.DetectedSchema;

        await next(context);
    }

    private async Task HandleValidationRequest(HttpContext context)
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

    private async Task HandleOptionsRequest(HttpContext context, TopicSettings? topic)
    {
        var schemaName =
            topic?.InputSchema == EventSchema.CloudEventV1_0 ? "CloudEventV10" : "EventGridEvent";

        context.Response.Headers.Append("Allow", "POST, OPTIONS");
        context.Response.Headers.Append("api-supported-versions", "2018-01-01");
        context.Response.Headers.Append("aeg-input-event-schema", schemaName);
        context.Response.Headers["x-ms-request-id"] = context.GetRequestId().ToString();
        context.Response.StatusCode = 200;
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
