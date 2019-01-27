using System.Net;

namespace AzureEventGridSimulator.Controllers
{
    public class ErrorMessage
    {
        public ErrorMessage(HttpStatusCode statusCode, string errorMessage)
        {
            Error = new ErrorDetails(statusCode, errorMessage);
        }

        public ErrorDetails Error { get; }

        public class ErrorDetails
        {
            internal ErrorDetails(HttpStatusCode statusCode, string errorMessage)
            {
                Code = statusCode.ToString();
                Message = errorMessage;
            }

            public string Message { get; }
            public string Code { get; }
        }
    }
}
