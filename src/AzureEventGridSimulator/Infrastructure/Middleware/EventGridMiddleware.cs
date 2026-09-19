using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using static AzureEventGridSimulator.Infrastructure.Extensions.HttpContextExtensions;

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
        TimeProvider timeProvider,
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
                    timeProvider,
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
                await context.WriteResourceNotFoundResponse();
                return;
        }
    }

    private async Task HandleNotificationRequest(
        HttpContext context,
        TopicSettings topic,
        SasKeyValidator sasKeyValidator,
        EventValidationOrchestrator validationOrchestrator,
        IEventHistoryService eventHistoryService,
        TimeProvider timeProvider,
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
        var eventValidation = await validationOrchestrator.ValidateEvents(
            context,
            topic,
            requestBody
        );

        if (!eventValidation.IsValid)
        {
            // Add Report suffix to error message if not already present
            // Azure does not add Report suffix for 413 (RequestEntityTooLarge) errors
            var errorMessage =
                eventValidation.ErrorMessage!.Contains("Report '", StringComparison.Ordinal)
                    ? eventValidation.ErrorMessage
                : eventValidation.StatusCode == HttpStatusCode.RequestEntityTooLarge
                    ? eventValidation.ErrorMessage
                : eventValidation.ErrorMessage + context.GenerateReportSuffix();

            // Record the rejection in event history
            var contentType = context.Request.Headers.ContentType.FirstOrDefault();
            RecordRejection(
                eventHistoryService,
                topic.Name,
                topic.Port,
                eventValidation.StatusCode!.Value,
                errorMessage,
                timeProvider.GetUtcNow(),
                requestBody,
                contentType
            );

            await context.WriteErrorResponse(
                eventValidation.StatusCode!.Value,
                errorMessage,
                null,
                eventValidation.ErrorCode
            );
            return;
        }

        // 4. Hand the topic and the parsed events to NotificationController
        context.SetValidatedPublish(
            topic,
            eventValidation.Events!,
            eventValidation.DetectedSchema!.Value
        );

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
        context.Response.Headers.Append("api-supported-versions", Constants.SupportedApiVersion);
        context.Response.Headers.Append("aeg-input-event-schema", schemaName);
        context.Response.Headers[RequestIdKey] = context.GetRequestId().ToString();
        context.Response.StatusCode = 200;
    }

    private static void RecordRejection(
        IEventHistoryService eventHistoryService,
        string topicName,
        int topicPort,
        HttpStatusCode statusCode,
        string errorMessage,
        DateTimeOffset rejectedAt,
        string? rawBody,
        string? contentType
    )
    {
        var rejection = RejectedEventRecord.Create(
            topicName,
            topicPort,
            statusCode,
            errorMessage,
            rejectedAt,
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
