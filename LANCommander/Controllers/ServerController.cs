using LANCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    public class ServerController : Controller
    {
        private readonly ServerService ServerService;

        public ServerController(ServerService serverService)
        {
            this.ServerService = serverService;
        }

        [HttpGet("/Server/{id:guid}/{*path}")]
        public async Task<IActionResult> Web(Guid id, string path)
        {
            var server = await ServerService.Get(id);

            if (server == null)
                return NotFound();

            path = path.Replace('/', Path.DirectorySeparatorChar);

            var filename = Path.Combine(server.HTTPRootPath, path);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", Path.GetFileName(filename));
        }
    }
}
