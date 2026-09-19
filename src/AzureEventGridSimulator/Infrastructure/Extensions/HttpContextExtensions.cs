using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

/// <summary>
///     A publish request that EventGridMiddleware has authenticated and validated, handed on to
///     NotificationController.
/// </summary>
/// <param name="Topic">The topic the request was routed to.</param>
/// <param name="Events">The parsed and validated events.</param>
/// <param name="Schema">The schema the events arrived in.</param>
public sealed record ValidatedPublish(
    TopicSettings Topic,
    SimulatorEvent[] Events,
    EventSchema Schema
);

public static class HttpContextExtensions
{
    /// <summary>
    ///     The x-ms-request-id response header. The request ID is also kept in HttpContext.Items
    ///     under this key.
    /// </summary>
    internal const string RequestIdKey = "x-ms-request-id";

    private static readonly object ValidatedPublishKey = new();

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
            // leaveOpen: the request body stream is owned by ASP.NET Core and may be re-read
            // after EnableBuffering(); disposing the reader must not close it.
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
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
            context.Response.Headers["api-supported-versions"] = Constants.SupportedApiVersion;
            context.Response.Headers[RequestIdKey] = requestId.ToString();

            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new ErrorMessage(statusCode, errorMessage, code, detailCode),
                    ErrorSerializerOptions
                )
            );
        }

        /// <summary>
        /// Stores the authenticated and validated publish request for NotificationController.
        /// </summary>
        public void SetValidatedPublish(
            TopicSettings topic,
            SimulatorEvent[] events,
            EventSchema schema
        )
        {
            context.Items[ValidatedPublishKey] = new ValidatedPublish(topic, events, schema);
        }

        /// <summary>
        /// Gets the publish request that EventGridMiddleware authenticated and validated.
        /// </summary>
        /// <exception cref="InvalidOperationException">EventGridMiddleware hasn't stored one.</exception>
        public ValidatedPublish GetValidatedPublish()
        {
            return
                context.Items.TryGetValue(ValidatedPublishKey, out var value)
                && value is ValidatedPublish publish
                ? publish
                : throw new InvalidOperationException(
                    "No validated publish request found in HttpContext. EventGridMiddleware must authenticate and validate the request before NotificationController runs."
                );
        }

        /// <summary>
        /// Writes the Azure-style 404 for a request URI that no resource matches.
        /// </summary>
        public Task WriteResourceNotFoundResponse()
        {
            return context.WriteErrorResponse(
                HttpStatusCode.NotFound,
                $"No HTTP resource was found that matches the request URI '{Uri.EscapeDataString($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}")}'.{context.GenerateReportSuffix()}",
                null,
                ErrorDetailCodes.ResourceNotFound
            );
        }
    }
}
