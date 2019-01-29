using System;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
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

            foreach (var eventGridEvent in events)
            {
                eventGridEvent.Topic = $"/subscriptions/{Guid.Empty:D}/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/{topicSettings.Name}";
                eventGridEvent.MetadataVersion = "1";
            }

            context.SaveEvents(events);
            context.SaveRequestBodyJson(JsonConvert.SerializeObject(events, Formatting.Indented));

            await _next(context);
        }
    }
}
