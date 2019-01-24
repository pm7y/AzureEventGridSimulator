using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;

namespace KestrelHttpsExample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        [HttpPost]
        public void Post([FromBody] EventGridEvent eventGridEvent)
        {
            try
            {
                eventGridEvent.Validate();
            }
            catch (InvalidOperationException)
            {
                Response.StatusCode = (int) HttpStatusCode.BadRequest;
            }
        }
    }
}
