using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Authorization;
using LANCommander.Services;
using System.Drawing;
using LANCommander.Models;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class GamesController : Controller
    {
        private readonly DatabaseContext Context;
        private readonly GameService GameService;
        private readonly ArchiveService ArchiveService;
        private readonly CategoryService CategoryService;
        private readonly TagService TagService;
        private readonly GenreService GenreService;

        public GamesController(DatabaseContext context, GameService gameService, ArchiveService archiveService, CategoryService categoryService, TagService tagService, GenreService genreService)
        {
            Context = context;
            GameService = gameService;
            ArchiveService = archiveService;
            CategoryService = categoryService;
            TagService = tagService;
            GenreService = genreService;
        }

        // GET: Games
        public async Task<IActionResult> Index()
        {
            return View(GameService.Get());
        }

        // GET: Games/Create
        public IActionResult Add()
        {
            return View();
        }

        // POST: Games/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind("Title,SortTitle,Description,ReleasedOn,Id,CreatedOn,CreatedById,UpdatedOn,UpdatedById")] Game game)
        {
            if (ModelState.IsValid)
            {
                await GameService.Add(game);

                return RedirectToAction(nameof(Index));
            }

            return View(game);
        }

        // GET: Games/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            Game game = await GameService.Get(id.GetValueOrDefault());

            if (game == null)
                return NotFound();

            return View(game);
        }

        // POST: Games/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Title,SortTitle,Description,ReleasedOn,Id,CreatedOn,CreatedById,UpdatedOn,UpdatedById")] Game game)
        {
            if (id != game.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await GameService.Update(game);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GameService.Exists(game.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            return View(game);
        }

        // GET: Games/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null || Context.Games == null)
            {
                return NotFound();
            }

            var game = await GameService.Get(id.GetValueOrDefault());

            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        // POST: Games/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            await GameService.Delete(game);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> GetIcon(Guid id)
        {
            try
            {
                var game = await GameService.Get(id);

                return File(GameService.GetIcon(game), "image/png");
            }
            catch (FileNotFoundException ex)
            {
                return NotFound();
            } 
        }
    }
}
