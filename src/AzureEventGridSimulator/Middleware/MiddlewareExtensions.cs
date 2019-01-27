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
    }
}
