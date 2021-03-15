using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Infrastructure.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestResponseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context,
                                      ILogger<RequestResponseLoggingMiddleware> logger)
        {
            var formattedRequest = await FormatRequestForLogging(context);
            logger.LogDebug("Request received {Request}", formattedRequest);

            await _next(context);
        }

        private static async Task<string> FormatRequestForLogging(HttpContext context)
        {
            context.Request.EnableBuffering();
            var request = await context.RequestBody();
            var x = new StringBuilder();
            x.AppendLine($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} {context.Request.Protocol}");

            foreach (var h in context.Request.Headers
                                     .Where(o => !string.IsNullOrWhiteSpace(o.Value.First())))
            {
                x.AppendLine($"{h.Key}: {h.Value.First()}");
            }

            x.AppendLine("");
            x.Append(request);
            return x.ToString();
        }
    }
}
