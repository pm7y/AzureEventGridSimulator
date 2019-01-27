using Microsoft.AspNetCore.Builder;

namespace AzureEventGridSimulator.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseAegSasHeaderValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegSasHeaderValidationMiddleware>();
        }

        public static IApplicationBuilder UseAegMessageValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegMessageValidationMiddleware>();
        }

        public static IApplicationBuilder UseAegTopicValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegTopicValidationMiddleware>();
        }

        public static IApplicationBuilder UseAeg(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AegRequestMiddleware>();
        }
    }
}
