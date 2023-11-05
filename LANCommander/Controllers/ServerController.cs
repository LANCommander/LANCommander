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

            if (server.HttpPaths == null || server.HttpPaths.Count == 0)
                return NotFound();

            // Sanitize
            if (path == null)
                path = "/";

            path = path.Trim('/');
            path = path + "/";

            var httpPath = server.HttpPaths.FirstOrDefault(hp => path.StartsWith(hp.Path.TrimStart('/')));

            // Check to see if there's a root path defined if nothing else matches
            if (httpPath == null)
                httpPath = server.HttpPaths.FirstOrDefault(hp => hp.Path == "/");

            if (httpPath == null)
                return Forbid();

            var relativePath = path.Substring(httpPath.Path.TrimStart('/').Length).Replace('/', Path.DirectorySeparatorChar).TrimStart('\\');

            var localPath = Path.Combine(httpPath.LocalPath, relativePath).TrimEnd('\\');
            var attrs = System.IO.File.GetAttributes(localPath);

            if ((attrs & FileAttributes.Directory) == FileAttributes.Directory)
            {
                if (!System.IO.Directory.Exists(localPath))
                    return NotFound();

                return Json(Directory.GetFileSystemEntries(localPath).Select(fse => fse.Substring(localPath.Length)));
            }
            else
            {
                if (!System.IO.File.Exists(localPath))
                    return NotFound();

                return File(new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", Path.GetFileName(localPath));
            }
        }
    }
}
