using AutoMapper;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace LANCommander.Server.Controllers
{
    public class ServerController : BaseController
    {
        private readonly ServerService ServerService;
        private readonly IMapper Mapper;

        public ServerController(
            ILogger<ServerController> logger,
            IMapper mapper,
            ServerService serverService) : base(logger)
        {
            ServerService = serverService;
            Mapper = mapper;
        }

        [HttpGet("/Server/{id:guid}/Export/Full")]
        public async Task ExportFull(Guid id)
        {
            var server = await ServerService.Get(id);

            if (server == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{server.Name}.lcx""");

            using (ZipArchive export = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create))
            {
                var manifest = Mapper.Map<SDK.Models.Server>(server);
                var serializedManifest = ManifestHelper.Serialize(manifest);

                using (var manistream = new MemoryStream())
                using (var sw = new StreamWriter(manistream))
                {
                    sw.Write(serializedManifest);
                    sw.Flush();

                    var manifestEntry = export.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);

                    using (var entryStream = manifestEntry.Open())
                    {
                        manistream.Seek(0, SeekOrigin.Begin);
                        manistream.CopyTo(entryStream);
                    }
                }

                if (server.Scripts != null)
                foreach (var script in server.Scripts)
                {
                    using (var scriptStream = new MemoryStream())
                    using (var sw = new StreamWriter(scriptStream))
                    {
                        sw.Write(script.Contents);
                        sw.Flush();

                        var scriptEntry = export.CreateEntry($"Scripts/{script.Id}", CompressionLevel.NoCompression);

                        using (var entryStream = scriptEntry.Open())
                        {
                            scriptStream.Seek(0, SeekOrigin.Begin);
                            scriptStream.CopyTo(entryStream);
                        }
                    }
                }

                var files = Directory.EnumerateFileSystemEntries($"{server.WorkingDirectory}", "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var entryName = file.Substring(server.WorkingDirectory.Length, file.Length - server.WorkingDirectory.Length).Replace(Path.DirectorySeparatorChar, '/');

                    if (System.IO.File.Exists(file))
                    {
                        export.CreateEntryFromFile(file, $"Files/{entryName}", CompressionLevel.NoCompression);
                    }
                    else if (System.IO.Directory.Exists(file))
                    {
                        export.CreateEntry($"Files/{entryName}/");
                    }
                }
            }
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
