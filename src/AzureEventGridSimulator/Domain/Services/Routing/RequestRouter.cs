using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Services.Routing;

/// <summary>
///     Type of request received.
/// </summary>
public enum RequestType
{
    /// <summary>
    ///     Notification event request (POST /api/events).
    /// </summary>
    Notification,

    /// <summary>
    ///     Subscription validation request (GET /validate?id=...).
    /// </summary>
    SubscriptionValidation,

    /// <summary>
    ///     Health check request (GET /api/health).
    /// </summary>
    Health,

    /// <summary>
    ///     Dashboard request (any /dashboard/* path).
    /// </summary>
    Dashboard,

    /// <summary>
    ///     OPTIONS preflight request (OPTIONS /api/events).
    /// </summary>
    OptionsPreFlight,

    /// <summary>
    ///     HEAD request to /api/events.
    /// </summary>
    HeadApiEvents,

    /// <summary>
    ///     Non-POST method to /api/events (returns 405).
    /// </summary>
    MethodNotAllowed,

    /// <summary>
    ///     Favicon request (browsers request this automatically).
    /// </summary>
    FaviconIgnore,

    /// <summary>
    ///     Unknown path (returns 404).
    /// </summary>
    NotFound,
}

/// <summary>
///     Result of request routing.
/// </summary>
/// <param name="Type">The type of request detected.</param>
/// <param name="Topic">The topic associated with this request (for notification requests).</param>
public record RequestRouteResult(RequestType Type, TopicSettings? Topic = null);

/// <summary>
///     Routes incoming HTTP requests to determine request type and extract associated topic.
/// </summary>
public class RequestRouter(SimulatorSettings simulatorSettings)
{
    /// <summary>
    ///     Routes an HTTP request to determine its type and extract the associated topic.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The routing result with request type and topic (if applicable).</returns>
    public RequestRouteResult RouteRequest(HttpContext context)
    {
        // Check for notification request (POST /api/events with appropriate content)
        if (IsNotificationRequest(context))
        {
            var topic = simulatorSettings.Topics.First(t => t.Port == context.Request.Host.Port);
            return new RequestRouteResult(RequestType.Notification, topic);
        }

        // Check for subscription validation request (GET /validate?id=...)
        if (IsValidationRequest(context))
        {
            return new RequestRouteResult(RequestType.SubscriptionValidation);
        }

        // Check for health check request (GET /api/health)
        if (IsHealthRequest(context))
        {
            return new RequestRouteResult(RequestType.Health);
        }

        // Check for dashboard request (/dashboard/*)
        if (IsDashboardRequest(context))
        {
            return new RequestRouteResult(RequestType.Dashboard);
        }

        // Favicon requests (browsers request this automatically)
        if (context.Request.Path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            return new RequestRouteResult(RequestType.FaviconIgnore);
        }

        // OPTIONS preflight request (CORS support)
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase)
            && context.Request.Method == HttpMethods.Options
        )
        {
            var topic = simulatorSettings.Topics.FirstOrDefault(t =>
                t.Port == context.Request.Host.Port
            );
            return new RequestRouteResult(RequestType.OptionsPreFlight, topic);
        }

        // HEAD request to /api/events (Azure returns 404)
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase)
            && context.Request.Method == HttpMethods.Head
        )
        {
            return new RequestRouteResult(RequestType.HeadApiEvents);
        }

        // Non-POST method to /api/events (Azure returns 405)
        if (
            string.Equals(context.Request.Path, "/api/events", StringComparison.Ordinal)
            && context.Request.Method != HttpMethods.Post
        )
        {
            return new RequestRouteResult(RequestType.MethodNotAllowed);
        }

        // Unknown path (returns 404)
        return new RequestRouteResult(RequestType.NotFound);
    }

    private static bool IsNotificationRequest(HttpContext context)
    {
        // Azure accepts paths in any case and with trailing slash
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        if (
            context.Request.Method != HttpMethods.Post
            || !string.Equals(path, "/api/events", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        // Check for CloudEvents binary mode (indicated by ce-* headers)
        if (IsCloudEventsBinaryMode(context))
        {
            return true;
        }

        var contentType = context.Request.Headers.ContentType.FirstOrDefault();

        // Azure Event Grid is lenient for EventGrid schema - accepts missing/wrong content-type
        // For CloudEvents, we'll validate the content-type later and return 415 if invalid
        // Accept all POST /api/events requests and let the schema detection/validation handle it
        if (string.IsNullOrWhiteSpace(contentType))
        {
            // Accept requests without Content-Type - EventGrid schema is lenient
            return true;
        }

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

    private static bool IsDashboardRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments(
            "/dashboard",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
