using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Middleware
{
    public static class HttpResponseExtensions
    {
        public static async Task ErrorResponse(this HttpResponse response, HttpStatusCode statusCode, string errorMessage)
        {
            var error = new ErrorMessage(HttpStatusCode.BadRequest, errorMessage);
            await response.WriteAsync(JsonConvert.SerializeObject(error, Formatting.Indented));

            response.Headers.Add("Content-type", "application/json");
            response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
}
