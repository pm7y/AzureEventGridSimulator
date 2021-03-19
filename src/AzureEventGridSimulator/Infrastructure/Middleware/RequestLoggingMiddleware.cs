using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Middleware
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task InvokeAsync(HttpContext context,
                                      ILogger<RequestLoggingMiddleware> logger)
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

            var redactedHeaders = new[] { HeaderNames.Authorization, Constants.AegSasKeyHeader, Constants.AegSasTokenHeader };

            foreach (var (key, stringValues) in context.Request.Headers
                                                                                      .Where(o => !string.IsNullOrWhiteSpace(o.Value.First())))
            {
                var value = redactedHeaders.Any(o => string.Equals(key, o, StringComparison.OrdinalIgnoreCase)) ? "--REDACTED--" : stringValues.First();

                x.AppendLine($"{key}: {value}");
            }

            x.AppendLine("");
            x.Append(request);
            return x.ToString();
        }
    }
}
