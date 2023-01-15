using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.SDK;
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

            if (game == null)
                return NotFound();

            Archive lastVersion = null;

            if (game.Archives != null && game.Archives.Count > 0)
                lastVersion = game.Archives.OrderByDescending(a => a.CreatedOn).First();

            return View(new Archive()
            {
                Game = game,
                GameId = game.Id,
                LastVersion = lastVersion,
            });
        }

        [HttpPost]
        public async Task<IActionResult> Add(Guid? id, Archive archive)
        {
            archive.Id = Guid.Empty;

            var game = await GameService.Get(id.GetValueOrDefault());

            if (game == null)
                return NotFound();

            archive.Game = game;
            archive.GameId = game.Id;

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

            var game = await GameService.Get(archive.GameId);

            if (game == null)
                return BadRequest("The related game is missing or corrupt.");

            archive.GameId = game.Id;
            archive.Id = Guid.Empty;
            archive.CompressedSize = compressedSize;
            archive.UncompressedSize = uncompressedSize;
            archive.ObjectKey = id.ToString();

            try
            {
                archive = await ArchiveService.Add(archive);
            }
            catch (Exception ex)
            {

            }

            return Json(new
            {
                Id = archive.Id,
                ObjectKey = archive.ObjectKey,
            });
        }
    }
}
