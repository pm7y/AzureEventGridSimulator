using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // Azure returns x-ms-request-id but NOT api-supported-versions for health endpoint
        HttpContext.Response.Headers["x-ms-request-id"] = HttpContext.GetRequestId().ToString();
        return Ok("OK");
    }
}
