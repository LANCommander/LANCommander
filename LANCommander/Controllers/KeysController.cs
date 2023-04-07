using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Authorization;
using LANCommander.Models;
using LANCommander.Services;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class KeysController : Controller
    {
        private readonly DatabaseContext Context;
        private readonly KeyService KeyService;

        public KeysController(DatabaseContext context, KeyService keyService)
        {
            Context = context;
            KeyService = keyService;
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            using (var repo = new Repository<Game>(Context, HttpContext))
            {
                var game = await repo.Find(id.GetValueOrDefault());

                if (game == null)
                    return NotFound();

                return View(game);
            }
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            using (var repo = new Repository<Game>(Context, HttpContext))
            {
                var game = await repo.Find(id.GetValueOrDefault());

                if (game == null)
                    return NotFound();

                var viewModel = new EditKeysViewModel()
                {
                    Game = game,
                    Keys = String.Join("\n", game.Keys.OrderByDescending(k => k.ClaimedOn).Select(k => k.Value))
                };

                return View(viewModel);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, EditKeysViewModel viewModel)
        {
            var keys = viewModel.Keys.Split("\n").Select(k => k.Trim()).Where(k => !String.IsNullOrWhiteSpace(k));

            using (var gameRepo = new Repository<Game>(Context, HttpContext))
            {
                var game = await gameRepo.Find(id);

                if (game == null)
                    return NotFound();

                using (var keyRepo = new Repository<Key>(Context, HttpContext))
                {
                    var existingKeys = keyRepo.Get(k => k.Game.Id == id).ToList();

                    var keysDeleted = existingKeys.Where(k => !keys.Contains(k.Value));
                    var keysAdded = keys.Where(k => !existingKeys.Any(e => e.Value == k));

                    foreach (var key in keysDeleted)
                        keyRepo.Delete(key);

                    foreach (var key in keysAdded)
                        await keyRepo.Add(new Key()
                        {
                            Game = game,
                            Value = key,
                        });

                    await keyRepo.SaveChanges();
                }
            }

            return RedirectToAction("Edit", "Games", new { id = id });
        }

        public async Task<IActionResult> Release(Guid id)
        {
            var existing = await KeyService.Get(id);

            if (existing == null)
                return NotFound();

            await KeyService.Release(id);

            return RedirectToAction("Details", "Keys", new { id = existing.Game.Id });
        }
    }
}
