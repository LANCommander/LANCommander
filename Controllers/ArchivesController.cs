using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize]
    public class ArchivesController : Controller
    {
        private readonly DatabaseContext Context;

        public ArchivesController(DatabaseContext context)
        {
            Context = context;
        }

        public async Task<IActionResult> Add(Guid? id)
        {
            if (id == null || Context.Games == null)
                return NotFound();

            using (Repository<Game> repo = new Repository<Game>(Context, HttpContext))
            {
                var game = await repo.Find(id.GetValueOrDefault());

                Archive lastVersion = null;

                if (game.Archives != null && game.Archives.Count > 0)
                    lastVersion = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

                return View(new Archive()
                {
                    Game = game,
                    LastVersion = lastVersion,
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Add(Guid? id, Archive archive)
        {
            archive.Id = Guid.Empty;

            using (Repository<Game> gameRepo = new Repository<Game>(Context, HttpContext))
            {
                var game = await gameRepo.Find(id.GetValueOrDefault());

                using (Repository<Archive> archiveRepo = new Repository<Archive>(Context, HttpContext))
                {
                    archive.Game = game;

                    archive = await archiveRepo.Add(archive);
                    await archiveRepo.SaveChanges();
                }
            }
            return RedirectToAction("Edit", "Games", new { id = id });
        }

        public async Task<IActionResult> Download(Guid id)
        {
            using (Repository<Archive> repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id);

                var content = new FileStream(Path.Combine("Upload", archive.ObjectKey), FileMode.Open, FileAccess.Read, FileShare.Read);

                return File(content, "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
            }
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            Guid gameId;

            using (Repository<Archive> repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id.GetValueOrDefault());

                gameId = archive.Game.Id;

                System.IO.File.Delete(Path.Combine("Upload", archive.ObjectKey));

                repo.Delete(archive);

                await repo.SaveChanges();
            }

            return RedirectToAction("Edit", "Games", new { id = gameId });
        }
    }
}
