using AzureEventGridSimulator.Infrastructure.Middleware;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseEventGridMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EventGridMiddleware>();
    }
}
