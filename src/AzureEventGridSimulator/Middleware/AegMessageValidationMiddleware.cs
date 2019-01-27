using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Controllers;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;

namespace AzureEventGridSimulator.Middleware
{
    public class AegMessageValidationMiddleware
    {
        private const int MaximumAllowedOverallMessageSizeInBytes = 1 * 1024 * 1024;
        private const int MaximumAllowedEventGridEventSizeInBytes = 64 * 1024;

        private readonly RequestDelegate _next;

        public AegMessageValidationMiddleware(RequestDelegate next)
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

            var requestBodyLength = requestBody.Length;

            if (requestBodyLength > MaximumAllowedOverallMessageSizeInBytes)
            {
                logger.Error("Payload is larger than the allowed maximum of 1Mb.");

                var error = new ErrorMessage(HttpStatusCode.BadRequest, "Payload is larger than the allowed maximum of 1Mb.");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                context.Response.Headers.Add("Content-type", "application/json");
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            var events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);

            foreach (var evt in events)
            {
                var eventJson = JsonConvert.SerializeObject(evt);
                var eventJsonLength = eventJson.Length;

                if (eventJsonLength > MaximumAllowedEventGridEventSizeInBytes)
                {
                    logger.Error("Event is larger than the allowed maximum of 64Kb.");

                    var error = new ErrorMessage(HttpStatusCode.BadRequest, "Event is larger than the allowed maximum of 64Kb.");
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                    context.Response.Headers.Add("Content-type", "application/json");
                    context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                    return;
                }

                try
                {
                    evt.Validate();
                }
                catch (InvalidOperationException ex)
                {
                    logger.Error(ex, "Event was not valid.");

                    var error = new ErrorMessage(HttpStatusCode.BadRequest, ex.Message);
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                    context.Response.Headers.Add("Content-type", "application/json");
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }

            var requestPort = context.Connection.LocalPort;
            var topic = simulatorSettings.Topics.First(t => t.HttpsPort == requestPort);
            var expectedTopicPath = $"/azure/event/grid/simulator/{topic.Name}";

            // Check that the topic is null or that it's valid
            foreach (var eventGridEvent in events)
            {
                if (!string.IsNullOrWhiteSpace(eventGridEvent.Topic))
                {
                    logger.Warning("'Topic' property was expected to be null or empty.");

                    var topicProperty = eventGridEvent.Topic.TrimEnd('/');

                    if (!string.Equals(topicProperty, expectedTopicPath, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Error($"Topic property should be null or {expectedTopicPath}");

                        var error = new ErrorMessage(HttpStatusCode.BadRequest, $"Topic property should be null or '{expectedTopicPath}'");
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

                        context.Response.Headers.Add("Content-type", "application/json");
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                }

                eventGridEvent.Topic = expectedTopicPath;
                eventGridEvent.MetadataVersion = "1";
            }

            await _next(context);
        }
    }
}
