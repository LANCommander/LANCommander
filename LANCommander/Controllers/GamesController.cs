using LANCommander.SDK.Helpers;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class GamesController : BaseController
    {
        private GameService GameService;
        private MediaService MediaService;
        private ArchiveService ArchiveService;

        public GamesController(GameService gameService)
        {
            GameService = gameService;
        }

        public async Task Export(Guid id)
        {
            var manifest = await GameService.GetManifest(id);

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{manifest.Title}.Export.zip""");

            using (ZipArchive export = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create))
            {
                var serializedManifest = ManifestHelper.Serialize(manifest);

                using (var manistream = new MemoryStream())
                using (var sw = new StreamWriter(manistream))
                {
                    sw.Write(serializedManifest);
                    var manifestEntry = export.CreateEntry("_manifest.yml", CompressionLevel.NoCompression);

                    using (var entryStream = manifestEntry.Open())
                    {
                        manistream.Seek(0, SeekOrigin.Begin);
                        manistream.CopyTo(entryStream);
                    }
                }

                if (manifest.Media != null)
                foreach (var media in manifest.Media)
                {
                    var mediaFilePath = await MediaService.GetImagePath(media.Id);
                    var entry = export.CreateEntry($"Media/{media.FileId}", CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(mediaFilePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                if (manifest.Archives != null)
                foreach (var archive in manifest.Archives)
                {
                    var archiveFilePath = ArchiveService.GetArchiveFileLocation(archive.ObjectKey);
                    var entry = export.CreateEntry($"Archives/{archive.ObjectKey}", CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(archiveFilePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> Import(List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if ((file.Length < 2 * 1024 * 1024) && file.Length > 0)
                {
                    try
                    {
                        using (var reader = new StreamReader(file.OpenReadStream()))
                        {
                            var content = await reader.ReadToEndAsync();

                            await GameService.Import(content);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex.Message);
                    }
                }
            }

            return Ok();
        }
    }
}
