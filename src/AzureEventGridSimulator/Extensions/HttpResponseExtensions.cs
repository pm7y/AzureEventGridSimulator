using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Extensions
{
    public static class HttpResponseExtensions
    {
        public static async Task ErrorResponse(this HttpResponse response, HttpStatusCode statusCode, string errorMessage)
        {
            var error = new ErrorMessage(statusCode, errorMessage);

            response.Headers.Add("Content-type", "application/json");

            response.StatusCode = (int)statusCode;
            await response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));
        }
    }
}
