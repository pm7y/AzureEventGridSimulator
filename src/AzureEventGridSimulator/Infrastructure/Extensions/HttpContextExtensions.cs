using System.IO;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class HttpContextExtensions
{
    public static async Task<string> RequestBody(this HttpContext context)
    {
        var reader = new StreamReader(context.Request.Body);
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        return await reader.ReadToEndAsync();
    }

    public static async Task WriteErrorResponse(this HttpContext context, HttpStatusCode statusCode, string errorMessage, string code)
    {
        var error = new ErrorMessage(statusCode, errorMessage, code);

        context.Response.Headers.Add(HeaderNames.ContentType, "application/json");

        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));
    }

    public static TopicSettings GetTopic(this HttpContext context)
    {
        return context.Features.Get<TopicSettings>();
    }

    public static void SetTopic(this HttpContext context, TopicSettings topic)
    {
        context.Features.Set(topic);
    }
}
