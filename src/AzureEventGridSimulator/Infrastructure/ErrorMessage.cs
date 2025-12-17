using System.Net;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure;

public class ErrorMessage
{
    public ErrorMessage(HttpStatusCode statusCode, string errorMessage, string code)
    {
        Error = new ErrorDetails(statusCode, errorMessage, code);
    }

    [JsonPropertyName("error")]
    public ErrorDetails Error { get; }

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
