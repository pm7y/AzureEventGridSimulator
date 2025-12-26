using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class HttpContextExtensions
{
    private const string RequestIdKey = "x-ms-request-id";

    private static readonly JsonSerializerOptions ErrorSerializerOptions = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    extension(HttpContext context)
    {
        public async Task<string> RequestBody()
        {
            using var reader = new StreamReader(context.Request.Body);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Gets or creates a request ID for the current request.
        /// This ID is used in both the x-ms-request-id header and error messages.
        /// </summary>
        public Guid GetRequestId()
        {
            if (context.Items.TryGetValue(RequestIdKey, out var existing) && existing is Guid id)
            {
                return id;
            }

            var newId = Guid.NewGuid();
            context.Items[RequestIdKey] = newId;
            return newId;
        }

        /// <summary>
        /// Generates the Azure-style report suffix for error messages.
        /// Uses the same request ID as the x-ms-request-id header.
        /// </summary>
        public string GenerateReportSuffix()
        {
            var requestId = context.GetRequestId();
            var timeProvider = context.RequestServices?.GetService<TimeProvider>();
            var timestamp = (timeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow).ToString(
                "M/d/yyyy h:mm:ss tt",
                System.Globalization.CultureInfo.InvariantCulture
            );
            return $" Report '{requestId}:1:{timestamp} (UTC)' to our forums for assistance or raise a support ticket.";
        }

        public async Task WriteErrorResponse(
            HttpStatusCode statusCode,
            string errorMessage,
            string? code,
            string? detailCode = null
        )
        {
            var requestId = context.GetRequestId();

            // Azure sets the 'api-supported-versions' header on error responses but does not include a 'Content-Type' header
            context.Response.Headers["api-supported-versions"] = "2018-01-01";
            context.Response.Headers["x-ms-request-id"] = requestId.ToString();

            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new ErrorMessage(statusCode, errorMessage, code, detailCode),
                    ErrorSerializerOptions
                )
            );
        }
    }
}
