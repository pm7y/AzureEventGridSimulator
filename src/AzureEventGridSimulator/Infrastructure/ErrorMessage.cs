using System.Net;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure;

public class ErrorMessage(HttpStatusCode statusCode, string errorMessage, string code)
{
    [JsonPropertyName("error")]
    public ErrorDetails Error { get; } = new(statusCode, errorMessage, code);

    public class ErrorDetails
    {
        internal ErrorDetails(HttpStatusCode statusCode, string errorMessage, string code)
        {
            Code = code ?? statusCode.ToString();
            Message = errorMessage;
        }

        [JsonPropertyName("code")]
        public string Code { get; }

        [JsonPropertyName("message")]
        public string Message { get; }
    }
}
