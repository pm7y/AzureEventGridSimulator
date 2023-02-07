namespace AzureEventGridSimulator.Infrastructure.Middleware
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AzureEventGridSimulator.Infrastructure.Extensions;
    using AzureEventGridSimulator.Infrastructure.Settings;
    using Microsoft.AspNetCore.Http;

    public sealed class TopicMiddleware : IMiddleware
    {
        private readonly SimulatorSettings _simulatorSettings;

        public TopicMiddleware(SimulatorSettings simulatorSettings)
        {
            _simulatorSettings = simulatorSettings;
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var port = context.Request.Host.Port;
            var topic = _simulatorSettings.Topics.FirstOrDefault(t => t.Port == port);
            if (topic == default)
            {
                throw new InvalidOperationException($"Topic not configured on port {port}");
            }

            context.SetTopic(topic);

            return next(context);
        }
    }
}
