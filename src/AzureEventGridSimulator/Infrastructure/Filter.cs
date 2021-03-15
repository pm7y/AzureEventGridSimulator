using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AzureEventGridSimulator.Infrastructure
{
    public class RequestLoggingAsyncActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            //var req = context.HttpContext.Request.ToString();

            // next() calls the action method.
            var resultContext = await next();
            // resultContext.Result is set.
            // Do something after the action executes.
        }
    }
}
