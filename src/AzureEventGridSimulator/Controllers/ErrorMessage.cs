using System.Net;

namespace AzureEventGridSimulator.Controllers
{
    public class ErrorMessage
    {
        public ErrorDetails Error { get; private set; }

        private ErrorMessage()
        {

        }

        public static ErrorMessage New(HttpStatusCode statusCode, string errorMessage)
        {
            return new ErrorMessage { Error = ErrorDetails.New(statusCode, errorMessage) };
        }

        public class ErrorDetails
        {
            public string Message { get; private set; }
            public HttpStatusCode Code { get; private set; }

            private ErrorDetails()
            {

            }

            public static ErrorDetails New(HttpStatusCode statusCode, string errorMessage)
            {
                return new ErrorDetails { Code = statusCode, Message = errorMessage };
            }
        }
    }
}