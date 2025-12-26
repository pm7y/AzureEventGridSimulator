using System.Net;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure;

/// <summary>
///     Error response format matching Azure Event Grid's error structure.
/// </summary>
public class ErrorMessage(
    HttpStatusCode statusCode,
    string errorMessage,
    string? code,
    string? detailCode = null
)
{
    [JsonPropertyName("error")]
    public ErrorDetails Error { get; } = new(statusCode, errorMessage, code, detailCode);

    public class ErrorDetails
    {
        internal ErrorDetails(
            HttpStatusCode statusCode,
            string errorMessage,
            string? code,
            string? detailCode
        )
        {
            Code = code ?? statusCode.ToString();
            Message = errorMessage;
            Details = [new ErrorDetail(detailCode ?? Code, errorMessage)];
        }

        [JsonPropertyName("code")]
        public string Code { get; }

        [JsonPropertyName("message")]
        public string Message { get; }

        [JsonPropertyName("details")]
        public ErrorDetail[] Details { get; }
    }

    public class ErrorDetail(string code, string message)
    {
        [JsonPropertyName("code")]
        public string Code { get; } = code;

        [JsonPropertyName("message")]
        public string Message { get; } = message;
    }
}

/// <summary>
///     Common Azure Event Grid error detail codes.
/// </summary>
public static class ErrorDetailCodes
{
    public const string InputJsonInvalid = "InputJsonInvalid";
    public const string InvalidContentType = "InvalidContentType";
    public const string ResourceNotFound = "ResourceNotFound";
    public const string Unauthorized = "Unauthorized";
    public const string InvalidSas = "InvalidSas";
    public const string PayloadTooLarge = "PayloadTooLarge";
    public const string InvalidCloudEventHeader = "InvalidCloudEventHeader";
    public const string MethodNotAllowed = "MethodNotAllowed";
}
