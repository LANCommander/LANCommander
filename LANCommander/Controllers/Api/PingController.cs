using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    public class PingController : Controller
    {
        public IActionResult Index()
        {
            return Ok("Pong!");
        }
    }
}
