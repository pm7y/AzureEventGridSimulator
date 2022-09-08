using Microsoft.AspNetCore.Mvc;

namespace AzureEventGridSimulator.Controllers;

[Route("/api/health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("OK");
    }
}
