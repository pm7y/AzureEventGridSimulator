using System;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public class AegTopicValidationMiddleware
    {
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

            foreach (var eventGridEvent in events)
            {
                if (!string.IsNullOrEmpty(eventGridEvent.Topic) && string.IsNullOrWhiteSpace(expectedTopicPath))
                {
                    logger.LogError("'Topic' property was expected to be null/empty.");

                    await context.Response.ErrorResponse(HttpStatusCode.BadRequest, $"Property 'topic' was found to be set to '{eventGridEvent.Topic}', but was expected to either be null/empty.");
                    return;
                }
                else if (!string.IsNullOrEmpty(eventGridEvent.Topic) && !string.IsNullOrWhiteSpace(expectedTopicPath))
                {
                    if (!string.Equals(eventGridEvent.Topic, expectedTopicPath, StringComparison.Ordinal))
                    {
                        logger.LogError("'Topic' property was expected to be null/empty or '{ExpectedTopicPath}'.", expectedTopicPath);

                        await context.Response.ErrorResponse(HttpStatusCode.BadRequest, $"Property 'topic' was found to be set to '{eventGridEvent.Topic}', but was expected to either be null/empty or be set to '{expectedTopicPath}'.");
                        return;
                    }
                }

                eventGridEvent.Topic = expectedTopicPath;
                eventGridEvent.MetadataVersion = "1";
            }

            context.SaveEvents(events);
            context.SaveRequestBodyJson(JsonConvert.SerializeObject(events));

            await _next(context);
        }
    }
}
