using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Detects the event schema from HTTP request headers and content type.
/// </summary>
public class EventSchemaDetector
{
    /// <summary>
    ///     Detects the event schema from the HTTP context.
    /// </summary>
    /// <param name="context">
    ///     The HTTP context.
    /// </param>
    /// <returns>
    ///     The detected event schema.
    /// </returns>
    public EventSchema DetectSchema(HttpContext context)
    {
        // Check for CloudEvents structured mode (content-type based)
        if (IsCloudEventStructuredMode(context))
        {
            return EventSchema.CloudEventV1_0;
        }

        // Check for CloudEvents binary mode (header based)
        if (IsBinaryMode(context))
        {
            return EventSchema.CloudEventV1_0;
        }

        // Default to EventGrid schema
        return EventSchema.EventGridSchema;
    }

    /// <summary>
    ///     Checks if the request is using CloudEvents structured content mode.
    ///     Structured mode uses the Content-Type header to indicate CloudEvents format.
    /// </summary>
    private bool IsCloudEventStructuredMode(HttpContext context)
    {
        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // Check for CloudEvents JSON content type (use base types for detection)
        return contentType.Contains(Constants.CloudEventsContentTypeBase, StringComparison.Ordinal)
            || contentType.Contains(
                Constants.CloudEventsBatchContentTypeBase,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    ///     Determines if the request is using CloudEvents binary mode.
    ///     Binary mode uses ce-* headers for CloudEvents attributes; see
    ///     <see cref="CloudEventsHttp.IsBinaryMode" />.
    /// </summary>
    public bool IsBinaryMode(HttpContext context)
    {
        return CloudEventsHttp.IsBinaryMode(context.Request.Headers);
    }

    /// <summary>
    ///     Determines if the request is a batch of CloudEvents.
    /// </summary>
    public bool IsBatchMode(HttpContext context)
    {
        var contentType = context.Request.ContentType;
        return !string.IsNullOrEmpty(contentType)
            && contentType.Contains(
                Constants.CloudEventsBatchContentTypeBase,
                StringComparison.Ordinal
            );
    }
}
