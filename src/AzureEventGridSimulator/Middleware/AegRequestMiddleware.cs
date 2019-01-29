using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public class AegRequestMiddleware
    {
        private readonly RequestDelegate _next;

        public AegRequestMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SimulatorSettings simulatorSettings, ILogger logger)
        {
            if (context.Request.Method != HttpMethods.Post ||
                !string.Equals(context.Request.Path, "/api/events", StringComparison.OrdinalIgnoreCase))
            {
                await context.Response.ErrorResponse(HttpStatusCode.BadRequest, "Not supported.");
                return;
            }

            var requestBody = await EnsureRequestBodyStreamIsWritable(context);

            var events = JsonConvert.DeserializeObject<EventGridEvent[]>(requestBody);
            var requestPort = context.Connection.LocalPort;
            var topicSettings = simulatorSettings.Topics.First(t => t.Port == requestPort);

            context.SaveRequestBodyJson(requestBody);
            context.SaveEvents(events);
            context.Items["TopicSettings"] = topicSettings;

            await _next(context);
        }

        private static async Task<string> EnsureRequestBodyStreamIsWritable(HttpContext context)
        {
            context.Request.EnableBuffering();
            var body = context.Request.Body;

            var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            var requestBody = Encoding.UTF8.GetString(buffer);
            context.Request.Body = body;
            return requestBody;
        }
    }
}
