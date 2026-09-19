using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;

namespace AzureEventGridSimulator.Domain.Services.Validation;

/// <summary>
///     Result of content-type validation.
/// </summary>
/// <param name="IsValid">Whether the validation passed.</param>
/// <param name="ErrorMessage">Error message if validation failed.</param>
/// <param name="StatusCode">HTTP status code for the error.</param>
/// <param name="ErrorCode">Error detail code.</param>
public record ContentTypeValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    HttpStatusCode? StatusCode = null,
    string? ErrorCode = null
);

/// <summary>
///     Validates Content-Type headers for CloudEvents schema compliance.
/// </summary>
public class ContentTypeValidator(ILogger<ContentTypeValidator> logger)
{
    private const string InvalidContentTypeMessage =
        "The Content-Type header is either missing or it doesn't have a valid value. "
        + "The content type header must either be application/cloudevents+json; charset=utf-8 "
        + "or application/cloudevents-batch+json; charset=UTF-8.";

    /// <summary>
    ///     Validates Content-Type headers for CloudEvents schema requests.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="detectedSchema">The detected event schema.</param>
    /// <returns>Validation result.</returns>
    public ContentTypeValidationResult ValidateContentType(
        HttpContext context,
        EventSchema detectedSchema
    )
    {
        var contentType = context.Request.Headers.ContentType.FirstOrDefault();

        // Only validate content-type for CloudEvents schema
        if (detectedSchema != EventSchema.CloudEventV1_0)
        {
            return new ContentTypeValidationResult(IsValid: true);
        }

        if (CloudEventsHttp.IsBinaryMode(context.Request.Headers))
        {
            // Binary mode with structured content type is a conflict - Azure returns 400
            if (IsValidCloudEventsContentType(contentType))
            {
                var errorMessage =
                    "Conflicting content mode: binary mode headers with structured mode content type.";

                logger.LogError(errorMessage);

                return new ContentTypeValidationResult(
                    IsValid: false,
                    ErrorMessage: errorMessage,
                    StatusCode: HttpStatusCode.BadRequest,
                    ErrorCode: ErrorDetailCodes.InputJsonInvalid
                );
            }

            // Binary mode: Content-Type represents the data's content type
            // Azure only accepts application/json for binary mode CloudEvents
            // Azure returns 415 for text/plain, application/octet-stream, etc.
            if (!IsApplicationJson(contentType))
            {
                logger.LogError(InvalidContentTypeMessage);

                return new ContentTypeValidationResult(
                    IsValid: false,
                    ErrorMessage: InvalidContentTypeMessage,
                    StatusCode: HttpStatusCode.UnsupportedMediaType,
                    ErrorCode: ErrorDetailCodes.InvalidContentType
                );
            }
        }
        else
        {
            // Structured/batch mode: require CloudEvents content types OR application/json
            // Azure accepts application/json and treats it as single CloudEvent (not batch)
            // It will fail with 400 if the body is an array instead of an object
            if (!IsValidCloudEventsContentType(contentType) && !IsApplicationJson(contentType))
            {
                logger.LogError(InvalidContentTypeMessage);

                return new ContentTypeValidationResult(
                    IsValid: false,
                    ErrorMessage: InvalidContentTypeMessage,
                    StatusCode: HttpStatusCode.UnsupportedMediaType,
                    ErrorCode: ErrorDetailCodes.InvalidContentType
                );
            }
        }

        return new ContentTypeValidationResult(IsValid: true);
    }

    private static bool IsValidCloudEventsContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

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
        {
            return false;
        }

        // Azure accepts application/json for CloudEvents: as a single event in structured mode,
        // and as the only data content type in binary mode
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
