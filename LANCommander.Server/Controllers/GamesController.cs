using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class GamesController : BaseController
    {
        private GameService GameService;
        private MediaService MediaService;

        public GamesController(GameService gameService, MediaService mediaService)
        {
            GameService = gameService;
            MediaService = mediaService;
        }

        [HttpGet("/Games/{id:guid}/Export/Full")]
        public async Task ExportFull(Guid id)
        {
            var game = await GameService.Get(id);

            if (game == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{game.Title}.lcx""");

            using (ZipArchive export = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create))
            {
                var manifest = await GameService.Export(game.Id);
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

                if (game.Media != null)
                foreach (var media in game.Media)
                {
                    var mediaFilePath = await MediaService.GetImagePath(media.Id);
                    var entry = export.CreateEntry($"Media/{media.FileId}", CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(mediaFilePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                if (game.Archives != null)
                foreach (var archive in game.Archives)
                {
                    var archiveFilePath = ArchiveService.GetArchiveFileLocation(archive.ObjectKey);
                    var entry = export.CreateEntry($"Archives/{archive.ObjectKey}", CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(archiveFilePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                if (game.Scripts != null)
                foreach (var script in game.Scripts)
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
            }
        }

        [HttpGet("/Games/{id:guid}/Export/Metadata")]
        public async Task ExportMetadata(Guid id)
        {
            var game = await GameService.Get(id);

            if (game == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{game.Title}.lcx""");

            using (ZipArchive export = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create))
            {
                var manifest = await GameService.Export(game.Id);

                manifest.Keys = null;
                manifest.Archives = null;

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

                if (game.Media != null)
                    foreach (var media in game.Media)
                    {
                        var mediaFilePath = await MediaService.GetImagePath(media.Id);
                        var entry = export.CreateEntry($"Media/{media.FileId}", CompressionLevel.NoCompression);

                        using (var entryStream = entry.Open())
                        using (var fileStream = System.IO.File.OpenRead(mediaFilePath))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }

                if (game.Scripts != null)
                    foreach (var script in game.Scripts)
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
            }
        }
    }
}
