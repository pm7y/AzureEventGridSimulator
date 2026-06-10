using System.Net;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Services.Validation;

/// <summary>
///     Reasons for body validation failure.
/// </summary>
public enum BodyValidationFailureReason
{
    /// <summary>
    ///     Empty body received in CloudEvents binary mode.
    /// </summary>
    EmptyBodyInBinaryMode,

    /// <summary>
    ///     Overall message size exceeds the maximum allowed.
    /// </summary>
    OverallSizeTooLarge,

    /// <summary>
    ///     Individual event size exceeds the maximum allowed.
    /// </summary>
    IndividualEventTooLarge,
}

/// <summary>
///     Result of body validation.
/// </summary>
/// <param name="IsValid">Whether the validation passed.</param>
/// <param name="ErrorMessage">Error message if validation failed.</param>
/// <param name="StatusCode">HTTP status code for the error.</param>
/// <param name="ErrorCode">Error detail code.</param>
/// <param name="FailureReason">Reason for validation failure.</param>
public record BodyValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    HttpStatusCode? StatusCode = null,
    string? ErrorCode = null,
    BodyValidationFailureReason? FailureReason = null
);

/// <summary>
///     Validates request body and event sizes against configured limits.
/// </summary>
public class RequestBodyValidator(ILogger<RequestBodyValidator> logger)
{
    /// <summary>
    ///     Validates the overall request body size and checks for empty body in binary mode.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="requestBody">The request body content.</param>
    /// <param name="limits">Validation limits to apply.</param>
    /// <returns>Validation result.</returns>
    public BodyValidationResult ValidateRequestBody(
        HttpContext context,
        string requestBody,
        EventValidationLimits limits
    )
    {
        // Azure returns 400 for empty body in binary mode CloudEvents
        if (IsCloudEventsBinaryMode(context) && string.IsNullOrWhiteSpace(requestBody))
        {
            return new BodyValidationResult(
                IsValid: false,
                ErrorMessage: "Unexpected end when reading JSON.",
                StatusCode: HttpStatusCode.BadRequest,
                ErrorCode: ErrorDetailCodes.InputJsonInvalid,
                FailureReason: BodyValidationFailureReason.EmptyBodyInBinaryMode
            );
        }

        // Validate the overall body size (the limit is in bytes, not UTF-16 chars)
        if (Encoding.UTF8.GetByteCount(requestBody) > limits.MaximumOverallMessageSizeInBytes)
        {
            logger.LogError("Payload is larger than the allowed maximum");

            return new BodyValidationResult(
                IsValid: false,
                ErrorMessage: $"The maximum size ({limits.MaximumOverallMessageSizeInBytes}) has been exceeded.",
                StatusCode: HttpStatusCode.RequestEntityTooLarge,
                ErrorCode: ErrorDetailCodes.PayloadTooLarge,
                FailureReason: BodyValidationFailureReason.OverallSizeTooLarge
            );
        }

        return new BodyValidationResult(IsValid: true);
    }

    /// <summary>
    ///     Validates the size of each individual event.
    /// </summary>
    /// <param name="events">The events to validate.</param>
    /// <param name="limits">Validation limits to apply.</param>
    /// <returns>Validation result.</returns>
    public BodyValidationResult ValidateEventSizes(
        SimulatorEvent[] events,
        EventValidationLimits limits
    )
    {
        foreach (var evt in events)
        {
            var eventSize =
                evt.Schema == EventSchema.EventGridSchema
                    ? JsonSerializer.SerializeToUtf8Bytes(evt.EventGridEvent).Length
                    : JsonSerializer.SerializeToUtf8Bytes(evt.CloudEvent).Length;

            if (eventSize > limits.MaximumEventSizeInBytes)
            {
                logger.LogError("Event is larger than the allowed maximum");

                return new BodyValidationResult(
                    IsValid: false,
                    ErrorMessage: $"The maximum size ({limits.MaximumEventSizeInBytes}) has been exceeded.",
                    StatusCode: HttpStatusCode.RequestEntityTooLarge,
                    ErrorCode: ErrorDetailCodes.PayloadTooLarge,
                    FailureReason: BodyValidationFailureReason.IndividualEventTooLarge
                );
            }
        }

        return new BodyValidationResult(IsValid: true);
    }

    private static bool IsCloudEventsBinaryMode(HttpContext context)
    {
        var headers = context.Request.Headers;
        // Binary mode is detected when any ce-* header is present
        return headers.ContainsKey(Constants.CeSpecVersionHeader)
            || headers.ContainsKey(Constants.CeIdHeader)
            || headers.ContainsKey(Constants.CeSourceHeader)
            || headers.ContainsKey(Constants.CeTypeHeader);
    }
}
