using AutoMapper;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace LANCommander.Server.Controllers
{
    public class ServerController : BaseController
    {
        private readonly ServerService ServerService;
        private readonly IMapper Mapper;
        private readonly IOptions<Settings.Settings> _settings;

        public ServerController(
            ILogger<ServerController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            IMapper mapper,
            ServerService serverService,
            IOptions<Settings.Settings> settings) : base(logger, settingsProvider)
        {
            ServerService = serverService;
            Mapper = mapper;
            _settings = settings;
        }

        [HttpGet("/Server/{id:guid}/{*path}")]
        public async Task<IActionResult> WebAsync(Guid id, string path)
        {
            var server = await ServerService
                .Include(s => s.HttpPaths)
                .GetAsync(id);

            if (server == null)
                return NotFound();

            if (server.Engine == Settings.Enums.ServerEngine.Remote)
            {
                var config = _settings.Value.Server.GameServers.ServerEngines
                    .FirstOrDefault(e => e.Id == server.RemoteHostId);

                if (config == null)
                    return NotFound();

                return Redirect($"{config.Address.TrimEnd('/')}/Server/{server.RemoteServerId}/{path}");
            }

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
