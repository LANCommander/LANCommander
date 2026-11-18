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
        private readonly IOptions<Settings.Settings> _settings;

        public ServerController(
            ILogger<ServerController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            ServerService serverService,
            IOptions<Settings.Settings> settings) : base(logger, settingsProvider)
        {
            ServerService = serverService;
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

            var root = Path.GetFullPath(httpPath.LocalPath);
            var relativePath = path.Substring(httpPath.Path.TrimStart('/').Length).Replace('/', Path.DirectorySeparatorChar).TrimStart('\\', '/');
            var localPath = Path.GetFullPath(Path.Combine(root, relativePath)).TrimEnd(Path.DirectorySeparatorChar);

            // Only what is inside the folder: "..", a backslash or a rooted path in the request mustn't
            // reach anything else on the machine
            if (!IsWithin(root, localPath))
                return NotFound();

            if (System.IO.Directory.Exists(localPath))
            {
                return Json(Directory.GetFileSystemEntries(localPath).Select(fse => fse.Substring(localPath.Length)));
            }
            else
            {
                if (!System.IO.File.Exists(localPath))
                    return NotFound();

                return File(new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", Path.GetFileName(localPath));
            }
        }

        /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or inside it; both full paths.</summary>
        internal static bool IsWithin(string root, string path)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return String.Equals(path, trimmedRoot, comparison)
                || path.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, comparison);
        }
    }
}
