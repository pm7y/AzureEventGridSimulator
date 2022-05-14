using System.Net;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure;

public class ErrorMessage
{
    public ErrorMessage(HttpStatusCode statusCode, string errorMessage, string code)
    {
        Error = new ErrorDetails(statusCode, errorMessage, code);
    }

    [JsonProperty(PropertyName = "error")]
    public ErrorDetails Error { get; }

    public class ErrorDetails
    {
        internal ErrorDetails(HttpStatusCode statusCode, string errorMessage, string code)
        {
            Code = code ?? statusCode.ToString();
            Message = errorMessage;
        }

        [JsonProperty(PropertyName = "code", Order = 1)]
        public string Code { get; }

        [JsonProperty(PropertyName = "message", Order = 2)]
        public string Message { get; }
    }
}
