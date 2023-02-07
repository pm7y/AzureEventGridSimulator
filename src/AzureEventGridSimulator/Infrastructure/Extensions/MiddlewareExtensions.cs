namespace AzureEventGridSimulator.Infrastructure.Extensions;

using System;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseNotFoundMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<NotFoundMiddleware>();
    }

    public static WebApplicationBuilder AddTopicRouting(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<TopicMiddleware>();
        builder.Services.AddSingleton<IStartupFilter, MiddlewareStartupFilter<TopicMiddleware>>();
        builder.Services.AddSingleton<MatcherPolicy, TopicMatcherPolicy>();

        return builder;
    }

    private sealed class MiddlewareStartupFilter<T> : IStartupFilter
        where T : IMiddleware
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // https://andrewlock.net/using-pathbase-with-dotnet-6-webapplicationbuilder/

            return builder =>
            {
                builder.UseMiddleware<T>();
                next(builder);
            };
        }
    }
}
