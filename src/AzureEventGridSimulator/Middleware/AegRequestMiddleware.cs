using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public class AegRequestMiddleware
    {
        private const int MaximumAllowedOverallMessageSizeInBytes = 1 * 1024 * 1024;

        private readonly RequestDelegate _next;

        public AegRequestMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, ILogger logger)
        {
            context.Request.EnableBuffering(MaximumAllowedOverallMessageSizeInBytes);
            var body = context.Request.Body;

            var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            var requestBody = Encoding.UTF8.GetString(buffer);
            context.Request.Body = body;

            logger.LogDebug(requestBody);

            var events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);
            var requestPort = context.Connection.LocalPort;
            var topicSettings = simulatorSettings.Topics.First(t => t.Port == requestPort);

            context.SaveRequestBodyJson(requestBody);
            context.SaveEvents(events);
            context.Items["TopicSettings"] = topicSettings;

            await _next(context);
        }
    }

    public static class HttpContextExtensions
    {
        public static EventGridEvent[] RetrieveEvents(this HttpContext httpContext)
        {
            return (EventGridEvent[])httpContext.Items["Events"];
        }

        public static void SaveEvents(this HttpContext httpContext, EventGridEvent[] events)
        {
            httpContext.Items["Events"] = events;
        }

        public static string RetrieveRequestBodyJson(this HttpContext httpContext)
        {
            return (string)httpContext.Items["RequestBody"];
        }

        public static void SaveRequestBodyJson(this HttpContext httpContext, string json)
        {
            httpContext.Items["RequestBody"] = json;
        }

        public static TopicSettings RetrieveTopicSettings(this HttpContext httpContext)
        {
            return (TopicSettings)httpContext.Items["TopicSettings"];
        }
    }
}
