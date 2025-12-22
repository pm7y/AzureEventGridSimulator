#nullable enable

using System.Reflection;

namespace AzureEventGridSimulator.Infrastructure.Dashboard;

/// <summary>
/// Middleware to serve embedded dashboard resources (HTML, CSS, JS).
/// </summary>
public class DashboardMiddleware
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

    private readonly Assembly _assembly;
    private readonly string _etagValue;
    private readonly ILogger<DashboardMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly string _resourcePrefix;

    public DashboardMiddleware(RequestDelegate next, ILogger<DashboardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _assembly = typeof(DashboardMiddleware).Assembly;
        _resourcePrefix = $"{_assembly.GetName().Name}.Dashboard.";

        // Generate ETag based on assembly MVID (Module Version ID) - unique per compilation
        // This works reliably in containers where file timestamps may not change
        var mvid = _assembly.ManifestModule.ModuleVersionId;
        _etagValue = $"\"{mvid}\"";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only handle /dashboard paths (but not /dashboard/api)
        if (
            !path.StartsWith("/dashboard", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/dashboard/api", StringComparison.OrdinalIgnoreCase)
        )
        {
            await _next(context);
            return;
        }

        // Determine the resource to serve
        var resourcePath =
            path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/dashboard/", StringComparison.OrdinalIgnoreCase)
                ? "index.html"
                : path["/dashboard/".Length..];

        // Security: prevent directory traversal
        if (resourcePath.Contains("..") || resourcePath.Contains("\\"))
        {
            context.Response.StatusCode = 400;
            return;
        }

        // Build the embedded resource name
        var resourceName = _resourcePrefix + resourcePath.Replace('/', '.');

        await using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogDebug("Dashboard resource not found: {ResourceName}", resourceName);
            context.Response.StatusCode = 404;
            return;
        }

        // Check If-None-Match header for conditional requests
        var requestEtag = context.Request.Headers.IfNoneMatch.FirstOrDefault();
        if (requestEtag == _etagValue)
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
        context.Response.Headers.ETag = _etagValue;
        context.Response.Headers.CacheControl = "no-cache"; // Always revalidate, but use cached if unchanged

        await stream.CopyToAsync(context.Response.Body);
    }
}

/// <summary>
/// Extension methods for registering dashboard middleware.
/// </summary>
public static class DashboardMiddlewareExtensions
{
    /// <summary>
    /// Adds the dashboard middleware to serve embedded UI resources.
    /// </summary>
    public static IApplicationBuilder UseDashboard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DashboardMiddleware>();
    }
}
