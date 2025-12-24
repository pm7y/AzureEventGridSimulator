using System.Net;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class HttpContextExtensions
{
    extension(HttpContext context)
    {
        public async Task<string> RequestBody()
        {
            using var reader = new StreamReader(context.Request.Body);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }

        public async Task WriteErrorResponse(
            HttpStatusCode statusCode,
            string errorMessage,
            string? code,
            string? detailCode = null
        )
        {
            context.Response.Headers[HeaderNames.ContentType] = "application/json";

            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new ErrorMessage(statusCode, errorMessage, code, detailCode),
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );
        }
    }
}
