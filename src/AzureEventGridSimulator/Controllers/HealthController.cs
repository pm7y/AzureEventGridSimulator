using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;
using static AzureEventGridSimulator.Infrastructure.Extensions.HttpContextExtensions;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // Azure returns x-ms-request-id but NOT api-supported-versions for health endpoint
        HttpContext.Response.Headers[RequestIdKey] = HttpContext.GetRequestId().ToString();
        return Ok("OK");
    }
}
