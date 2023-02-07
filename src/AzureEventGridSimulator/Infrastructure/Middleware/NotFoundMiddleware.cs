namespace AzureEventGridSimulator.Infrastructure.Middleware;

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;

internal sealed class NotFoundMiddleware : IMiddleware
{
    private static readonly int[] _statusCodes = new[]
    {
            (int)HttpStatusCode.NotFound,
            (int)HttpStatusCode.MethodNotAllowed
        };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);

        if (!context.Response.HasStarted && _statusCodes.Contains(context.Response.StatusCode))
        {
            await context.WriteErrorResponse(HttpStatusCode.BadRequest, "Request not supported.", null);
        }
    }
}
