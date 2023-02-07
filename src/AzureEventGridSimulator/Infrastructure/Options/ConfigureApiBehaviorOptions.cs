namespace AzureEventGridSimulator.Infrastructure.Options;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using AzureEventGridSimulator.Domain.Converters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed class ConfigureApiBehaviorOptions : IConfigureOptions<ApiBehaviorOptions>
{
    private static readonly IReadOnlyDictionary<string, HttpStatusCode> _errorCodes;

    static ConfigureApiBehaviorOptions()
    {
        // TODO: this needs a cleaner solution
        _errorCodes = new Dictionary<string, HttpStatusCode>()
        {
            { CloudEventConverter.MaximumAllowedEventGridEventSizeErrorMesage, HttpStatusCode.RequestEntityTooLarge },
            //{ EventGridEventConverter.MaximumAllowedEventGridEventSizeErrorMesage, HttpStatusCode.RequestEntityTooLarge },
        };
    }

    public void Configure(ApiBehaviorOptions options)
    {
        static IActionResult Error(HttpStatusCode statusCode, string message, string code = null)
        {
            return new ObjectResult(new ErrorMessage(statusCode, message, code))
            {
                StatusCode = (int)statusCode,
                ContentTypes = { "application/json" }
            };
        }

        options.InvalidModelStateResponseFactory = context =>
        {
            var error = context.ModelState.Values.FirstOrDefault(x => x.Errors.Count > 0)?.Errors.First();
            if (error == null)
            {
                return Error(HttpStatusCode.InternalServerError, "An internal server error occurred.");
            }

            var statusCode = _errorCodes.TryGetValue(error.ErrorMessage, out var value) ? value : HttpStatusCode.BadRequest;
            return Error(statusCode, error.ErrorMessage);
        };
    }
}
