using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Extensions
{
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
