using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Services.Validation;

/// <summary>
///     Stage at which validation failed.
/// </summary>
public enum ValidationFailureStage
{
    /// <summary>
    ///     Failed at body size validation.
    /// </summary>
    BodySize,

    /// <summary>
    ///     Failed at content-type validation.
    /// </summary>
    ContentType,

    /// <summary>
    ///     Failed at event parsing.
    /// </summary>
    Parsing,

    /// <summary>
    ///     Failed at event validation (properties, topic authorization).
    /// </summary>
    EventValidation,

    /// <summary>
    ///     Failed at individual event size validation.
    /// </summary>
    EventSize,
}

/// <summary>
///     Result of complete event validation pipeline.
/// </summary>
/// <param name="IsValid">Whether validation passed.</param>
/// <param name="Events">Parsed events if validation succeeded.</param>
/// <param name="DetectedSchema">Detected schema if validation succeeded.</param>
/// <param name="ErrorMessage">Error message if validation failed.</param>
/// <param name="StatusCode">HTTP status code for the error.</param>
/// <param name="ErrorCode">Error detail code.</param>
/// <param name="FailureStage">Stage at which validation failed.</param>
public record EventValidationResult(
    bool IsValid,
    SimulatorEvent[]? Events = null,
    EventSchema? DetectedSchema = null,
    string? ErrorMessage = null,
    HttpStatusCode? StatusCode = null,
    string? ErrorCode = null,
    ValidationFailureStage? FailureStage = null
);

/// <summary>
///     Orchestrates the complete event validation pipeline.
/// </summary>
public class EventValidationOrchestrator(
    EventSchemaDetector schemaDetector,
    EventSchemaParserFactory parserFactory,
    RequestBodyValidator bodyValidator,
    ContentTypeValidator contentTypeValidator,
    SimulatorSettings simulatorSettings,
    ILogger<EventValidationOrchestrator> logger
)
{
    /// <summary>
    ///     Validates events through the complete validation pipeline.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="topic">The topic configuration.</param>
    /// <param name="requestBody">The request body content.</param>
    /// <returns>Validation result with parsed events or error details.</returns>
    public async Task<EventValidationResult> ValidateEvents(
        HttpContext context,
        TopicSettings topic,
        string requestBody
    )
    {
        var limits = simulatorSettings.EventValidationLimits;

        // 1. Validate request body size
        var bodyResult = bodyValidator.ValidateRequestBody(context, requestBody, limits);
        if (!bodyResult.IsValid)
        {
            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: bodyResult.ErrorMessage,
                StatusCode: bodyResult.StatusCode,
                ErrorCode: bodyResult.ErrorCode,
                FailureStage: ValidationFailureStage.BodySize
            );
        }

        // 2. Detect schema (use configured input schema or auto-detect)
        var detectedSchema = topic.InputSchema ?? schemaDetector.DetectSchema(context);

        // 3. Validate content-type for CloudEvents schema
        var contentTypeResult = contentTypeValidator.ValidateContentType(context, detectedSchema);
        if (!contentTypeResult.IsValid)
        {
            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: contentTypeResult.ErrorMessage,
                StatusCode: contentTypeResult.StatusCode,
                ErrorCode: contentTypeResult.ErrorCode,
                FailureStage: ValidationFailureStage.ContentType
            );
        }

        // 4. Parse events
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

            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: ex.Message,
                StatusCode: HttpStatusCode.BadRequest,
                ErrorCode: errorCode,
                FailureStage: ValidationFailureStage.Parsing
            );
        }

        // 5. Validate events array is not empty
        if (events == null || events.Length == 0)
        {
            var schemaName =
                detectedSchema == EventSchema.CloudEventV1_0 ? "CloudEventV10" : "EventGridEvent";
            var errorMessage =
                $"This resource is configured to receive event in '{schemaName}' schema. "
                + "The JSON received does not conform to the expected schema.";

            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: errorMessage,
                StatusCode: HttpStatusCode.BadRequest,
                ErrorCode: ErrorDetailCodes.InputJsonInvalid,
                FailureStage: ValidationFailureStage.Parsing
            );
        }

        // 6. Validate individual event sizes
        var eventSizeResult = bodyValidator.ValidateEventSizes(events, limits);
        if (!eventSizeResult.IsValid)
        {
            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: eventSizeResult.ErrorMessage,
                StatusCode: eventSizeResult.StatusCode,
                ErrorCode: eventSizeResult.ErrorCode,
                FailureStage: ValidationFailureStage.EventSize
            );
        }

        // 7. Validate event properties
        try
        {
            parser.Validate(events);
        }
        catch (TopicAuthorizationException ex)
        {
            // Azure returns 401 when topic field is set (doesn't match endpoint)
            logger.LogError(ex, "Topic authorization failed");

            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: ex.Message,
                StatusCode: HttpStatusCode.Unauthorized,
                ErrorCode: ErrorDetailCodes.Unauthorized,
                FailureStage: ValidationFailureStage.EventValidation
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Event was not valid");

            return new EventValidationResult(
                IsValid: false,
                ErrorMessage: ex.Message,
                StatusCode: HttpStatusCode.BadRequest,
                ErrorCode: ErrorDetailCodes.InputJsonInvalid,
                FailureStage: ValidationFailureStage.EventValidation
            );
        }

        // All validation passed
        return new EventValidationResult(
            IsValid: true,
            Events: events,
            DetectedSchema: detectedSchema
        );
    }
}
