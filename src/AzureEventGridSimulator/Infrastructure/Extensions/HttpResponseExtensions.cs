using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class HttpResponseExtensions
    {
        public static async Task ErrorResponse(this HttpResponse response, HttpStatusCode statusCode, string errorMessage, string code)
        {
            var error = new ErrorMessage(statusCode, errorMessage, code);

            response.Headers.Add("Content-type", "application/json");

            response.StatusCode = (int)statusCode;
            // ReSharper disable once MethodHasAsyncOverload
            await response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));
        }
    }
}
