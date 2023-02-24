namespace AzureEventGridSimulator.Infrastructure.Filters;

using System;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class MaxContentLengthAttribute : ActionFilterAttribute
{
    private const int MaximumAllowedOverallMessageSizeInBytes = 1536000;
    private readonly ILogger _logger;

    public MaxContentLengthAttribute(ILogger<MaxContentLengthAttribute> logger)
    {
        _logger = logger;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > MaximumAllowedOverallMessageSizeInBytes)
        {
            _logger.LogError("Payload is larger than the allowed maximum");

            await context.HttpContext.WriteErrorResponse(HttpStatusCode.RequestEntityTooLarge, $"The maximum size ({MaximumAllowedOverallMessageSizeInBytes}) has been exceeded.", null);
            return;
        }

        await base.OnActionExecutionAsync(context, next);
    }
}
