using System.Reflection;

namespace AzureEventGridSimulator.Infrastructure.Dashboard;

/// <summary>
///     Middleware to serve embedded dashboard resources (HTML, CSS, JS).
/// </summary>
public class DashboardMiddleware(RequestDelegate next, ILogger<DashboardMiddleware> logger)
{
    private static readonly Dictionary<string, string> ContentTypes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { ".html", "text/html; charset=utf-8" },
        { ".css", "text/css; charset=utf-8" },
        { ".js", "application/javascript; charset=utf-8" },
        { ".json", "application/json; charset=utf-8" },
        { ".png", "image/png" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
    };

    private static readonly Assembly ResourceAssembly = typeof(DashboardMiddleware).Assembly;

    private static readonly string ResourcePrefix = $"{ResourceAssembly.GetName().Name}.Dashboard.";

    // Generate ETag based on assembly MVID (Module Version ID) - unique per compilation
    // This works reliably in containers where file timestamps may not change
    private static readonly string ETagValue =
        $"\"{ResourceAssembly.ManifestModule.ModuleVersionId}\"";

    public async Task InvokeAsync(HttpContext context)
    {
        // Only handle the /dashboard segment, matched the same way as RequestRouter. Anything
        // starting /dashboard/api (a plain prefix, as before) is left to the API endpoints.
        if (
            !context.Request.Path.StartsWithSegments(
                "/dashboard",
                StringComparison.OrdinalIgnoreCase,
                out var remaining
            )
            || remaining.Value?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true
        )
        {
            await next(context);
            return;
        }

        // Determine the resource to serve. remaining is "" for /dashboard and otherwise starts
        // with '/'. Drop exactly one '/', so /dashboard//styles.css still isn't an asset.
        var remainingPath = remaining.Value ?? string.Empty;
        var resourcePath = remainingPath is "" or "/" ? "index.html" : remainingPath[1..];

        // Security: prevent directory traversal
        if (
            resourcePath.Contains("..", StringComparison.Ordinal)
            || resourcePath.Contains("\\", StringComparison.Ordinal)
        )
        {
            context.Response.StatusCode = 400;
            return;
        }

        // Build the embedded resource name
        var resourceName = ResourcePrefix + resourcePath.Replace('/', '.');

        await using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            logger.LogDebug("Dashboard resource not found: {ResourceName}", resourceName);
            context.Response.StatusCode = 404;
            return;
        }

        // Check If-None-Match header for conditional requests
        var requestEtag = context.Request.Headers.IfNoneMatch.FirstOrDefault();
        if (requestEtag == ETagValue)
        {
            context.Response.StatusCode = 304; // Not Modified
            return;
        }

        // Set content type based on file extension
        var extension = Path.GetExtension(resourcePath);
        context.Response.ContentType = ContentTypes.GetValueOrDefault(
            extension,
            "application/octet-stream"
        );

        // Set ETag and cache headers
        context.Response.Headers.ETag = ETagValue;
        context.Response.Headers.CacheControl = "no-cache"; // Always revalidate, but use cached if unchanged

        await stream.CopyToAsync(context.Response.Body);
    }
}

/// <summary>
///     Extension methods for registering dashboard middleware.
/// </summary>
public static class DashboardMiddlewareExtensions
{
    /// <summary>
    ///     Adds the dashboard middleware to serve embedded UI resources.
    /// </summary>
    public static IApplicationBuilder UseDashboard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DashboardMiddleware>();
    }
}
