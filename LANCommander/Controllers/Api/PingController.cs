using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class PingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Ok("Pong!");
        }
    }
}
