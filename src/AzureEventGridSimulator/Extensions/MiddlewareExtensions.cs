using AzureEventGridSimulator.Middleware;
using Microsoft.AspNetCore.Builder;

namespace AzureEventGridSimulator.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseAegSasHeaderValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegSasHeaderValidationMiddleware>();
        }

        public static IApplicationBuilder UseAegSizeValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegSizeValidationMiddleware>();
        }

        public static IApplicationBuilder UseAegTopicValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegTopicValidationMiddleware>();
        }

        public static IApplicationBuilder UseAeg(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegRequestMiddleware>();
        }

        public static IApplicationBuilder UseAegEventValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegEventValidationMiddleware>();
        }
    }
}
