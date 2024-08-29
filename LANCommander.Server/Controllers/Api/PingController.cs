using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class PingController : BaseApiController
    {
        public PingController(ILogger<PingController> logger) : base(logger) { }

        [HttpGet]
        public IActionResult Index()
        {
            return Ok("Pong!");
        }
    }
}
