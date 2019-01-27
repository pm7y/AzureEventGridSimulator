using Microsoft.AspNetCore.Builder;

namespace AzureEventGridSimulator.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseAegHeaderValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegHeaderValidationMiddleware>();
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
