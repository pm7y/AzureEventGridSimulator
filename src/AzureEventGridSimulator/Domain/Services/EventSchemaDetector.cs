using AzureEventGridSimulator.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Detects the event schema from HTTP request headers and content type.
/// </summary>
public class EventSchemaDetector
{
    /// <summary>
    /// Detects the event schema from the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The detected event schema.</returns>
    public EventSchema DetectSchema(HttpContext context)
    {
        // Check for CloudEvents structured mode (content-type based)
        if (IsCloudEventStructuredMode(context))
        {
            return EventSchema.CloudEventV1_0;
        }

        // Check for CloudEvents binary mode (header based)
        if (IsCloudEventBinaryMode(context))
        {
            return EventSchema.CloudEventV1_0;
        }

        // Default to EventGrid schema
        return EventSchema.EventGridSchema;
    }

    /// <summary>
    /// Checks if the request is using CloudEvents structured content mode.
    /// Structured mode uses the Content-Type header to indicate CloudEvents format.
    /// </summary>
    private bool IsCloudEventStructuredMode(HttpContext context)
    {
        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // Check for CloudEvents JSON content type (use base types for detection)
        return contentType.Contains(Constants.CloudEventsContentTypeBase) ||
               contentType.Contains(Constants.CloudEventsBatchContentTypeBase);
    }

    /// <summary>
    /// Checks if the request is using CloudEvents binary content mode.
    /// Binary mode uses ce-* headers for CloudEvents attributes.
    /// </summary>
    private bool IsCloudEventBinaryMode(HttpContext context)
    {
        var headers = context.Request.Headers;

        // Binary mode requires the four required CloudEvents headers
        return headers.ContainsKey(Constants.CeSpecVersionHeader) &&
               headers.ContainsKey(Constants.CeIdHeader) &&
               headers.ContainsKey(Constants.CeSourceHeader) &&
               headers.ContainsKey(Constants.CeTypeHeader);
    }

    /// <summary>
    /// Determines if the request is using CloudEvents binary mode.
    /// </summary>
    public bool IsBinaryMode(HttpContext context)
    {
        return IsCloudEventBinaryMode(context);
    }

    /// <summary>
    /// Determines if the request is using CloudEvents structured mode.
    /// </summary>
    public bool IsStructuredMode(HttpContext context)
    {
        return IsCloudEventStructuredMode(context);
    }

    /// <summary>
    /// Determines if the request is a batch of CloudEvents.
    /// </summary>
    public bool IsBatchMode(HttpContext context)
    {
        var contentType = context.Request.ContentType;
        return !string.IsNullOrEmpty(contentType) &&
               contentType.Contains(Constants.CloudEventsBatchContentTypeBase);
    }
}
