using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Controllers;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public class AegTopicValidationMiddleware
    {
        private const int MaximumAllowedOverallMessageSizeInBytes = 1 * 1024 * 1024;

        private readonly RequestDelegate _next;

        public AegTopicValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, ILogger logger)
        {
            var events = context.RetrieveEvents();
            var topicSettings = context.RetrieveTopicSettings();
            var expectedTopicPath = topicSettings.ExpectedTopicUri;

            // Check that the topic is null or that it's valid
            foreach (var eventGridEvent in events)
            {
                if (!string.IsNullOrEmpty(eventGridEvent.Topic) && string.IsNullOrWhiteSpace(expectedTopicPath))
                {
                    logger.LogError("'Topic' property was expected to be null or empty.");

                    await context.Response.ErrorResponse(HttpStatusCode.BadRequest, $"Property 'topic' was found to be set to '{eventGridEvent.Topic}', but was expected to either be null/empty.");
                    return;
                }
                else if (!string.IsNullOrWhiteSpace(expectedTopicPath))
                {
                    if (!string.Equals(eventGridEvent.Topic, expectedTopicPath, StringComparison.Ordinal))
                    {
                        logger.LogError("'Topic' property should be '{ExpectedTopicPath}'.", expectedTopicPath);

                        await context.Response.ErrorResponse(HttpStatusCode.BadRequest, $"Property 'topic' was found to be set to '{eventGridEvent.Topic}', but was expected to either be null/empty or be set to '{expectedTopicPath}'.");
                        return;
                    }
                }

                eventGridEvent.Topic = expectedTopicPath;
                eventGridEvent.MetadataVersion = "1";
            }

            await _next(context);
        }
    }

    public static class HttpResponseExtensions
    {
        public static async Task ErrorResponse(this HttpResponse response, HttpStatusCode statusCode, string errorMessage)
        {
            var error = new ErrorMessage(HttpStatusCode.BadRequest, errorMessage);
            await response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

            response.Headers.Add("Content-type", "application/json");
            response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
}
