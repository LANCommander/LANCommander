using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ArchivesController : Controller
    {
        private readonly GameService GameService;
        private readonly ArchiveService ArchiveService;

        public ArchivesController(GameService gameService, ArchiveService archiveService)
        {
            GameService = gameService;
            ArchiveService = archiveService;
        }

        public async Task<IActionResult> Add(Guid? id)
        {
            if (id == null)
                return NotFound();

            var game = await GameService.Get(id.GetValueOrDefault());

            Archive lastVersion = null;

            if (game.Archives != null && game.Archives.Count > 0)
                lastVersion = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

            return View(new Archive()
            {
                Game = game,
                LastVersion = lastVersion,
            });
        }

        [HttpPost]
        public async Task<IActionResult> Add(Guid? id, Archive archive)
        {
            archive.Id = Guid.Empty;

            var game = await GameService.Get(id.GetValueOrDefault());

            archive.Game = game;

            if (game.Archives != null && game.Archives.Any(a => a.Version == archive.Version))
                ModelState.AddModelError("Version", "An archive for this game is already using that version.");

            if (ModelState.IsValid)
            {
                await ArchiveService.Update(archive);

                return RedirectToAction("Edit", "Games", new { id = id });
            }

            return View(archive);
        }

        public async Task<IActionResult> Download(Guid id)
        {
            var archive = await ArchiveService.Get(id);

            var content = new FileStream($"Upload/{archive.ObjectKey}".ToPath(), FileMode.Open, FileAccess.Read, FileShare.Read);

            return File(content, "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            var archive = await ArchiveService.Get(id.GetValueOrDefault());
            var gameId = archive.Game.Id;

            await ArchiveService.Delete(archive);

            return RedirectToAction("Edit", "Games", new { id = gameId });
        }

        public async Task<IActionResult> Validate(Guid id, Archive archive)
        {
            var path = $"Upload/{id}".ToPath();

            string manifestContents = String.Empty;
            long compressedSize = 0;
            long uncompressedSize = 0;

            if (!System.IO.File.Exists(path))
                return BadRequest("Specified object does not exist");

            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    var manifest = zip.Entries.FirstOrDefault(e => e.FullName == "_manifest.yml");

                    if (manifest == null)
                        throw new FileNotFoundException("Manifest file not found. Add a _manifest.yml file to your archive and try again.");

                    using (StreamReader sr = new StreamReader(manifest.Open()))
                    {
                        manifestContents = await sr.ReadToEndAsync();
                    }

                    compressedSize = zip.Entries.Sum(e => e.CompressedLength);
                    uncompressedSize = zip.Entries.Sum(e => e.Length);
                }
            }
            catch (InvalidDataException ex)
            {
                System.IO.File.Delete(path);
                return BadRequest("Uploaded archive is corrupt or not a .zip file.");
            }
            catch (FileNotFoundException ex)
            {
                System.IO.File.Delete(path);
                return BadRequest(ex.Message);
            }
            catch
            {
                System.IO.File.Delete(path);
                return BadRequest("An unknown error occurred.");
            }

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            try
            {
                var manifest = deserializer.Deserialize<GameManifest>(manifestContents);
            }
            catch
            {
                System.IO.File.Delete(path);
                return BadRequest("The manifest file is invalid or corrupt.");
            }

            var game = await GameService.Get(archive.Game.Id);

            if (game == null)
                return BadRequest("The related game is missing or corrupt.");

            archive.Game = game;
            archive.Id = Guid.Empty;
            archive.CompressedSize = compressedSize;
            archive.UncompressedSize = uncompressedSize;
            archive.ObjectKey = id.ToString();

            archive = await ArchiveService.Update(archive);

            return Json(new
            {
                Id = archive.Id,
                ObjectKey = archive.ObjectKey,
            });
        }
    }
}
