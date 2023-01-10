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
using LANCommander.Data.Enums;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class GamesController : Controller
    {
        private readonly GameService GameService;
        private readonly ArchiveService ArchiveService;
        private readonly CategoryService CategoryService;
        private readonly TagService TagService;
        private readonly GenreService GenreService;
        private readonly CompanyService CompanyService;
        private readonly IGDBService IGDBService;

        public GamesController(GameService gameService, ArchiveService archiveService, CategoryService categoryService, TagService tagService, GenreService genreService, CompanyService companyService, IGDBService igdbService)
        {
            GameService = gameService;
            ArchiveService = archiveService;
            CategoryService = categoryService;
            TagService = tagService;
            GenreService = genreService;
            CompanyService = companyService;
            IGDBService = igdbService;
        }

        // GET: Games
        public async Task<IActionResult> Index()
        {
            return View(GameService.Get());
        }

        public async Task<IActionResult> Add(long? igdbid)
        {
            var viewModel = new GameViewModel()
            {
                Game = new Game(),
                Developers = new List<SelectListItem>(),
                Publishers = new List<SelectListItem>(),
                Genres = new List<SelectListItem>(),
                Tags = new List<SelectListItem>(),
            };

            if (igdbid == null)
            {
                viewModel.Game = new Game();
                viewModel.Developers = CompanyService.Get().OrderBy(c => c.Name).Select(c => new SelectListItem() { Text = c.Name, Value = c.Name }).ToList();
                viewModel.Publishers = CompanyService.Get().OrderBy(c => c.Name).Select(c => new SelectListItem() { Text = c.Name, Value = c.Name }).ToList();
                viewModel.Genres = GenreService.Get().OrderBy(g => g.Name).Select(g => new SelectListItem() { Text = g.Name, Value = g.Name }).ToList();
                viewModel.Tags = TagService.Get().OrderBy(t => t.Name).Select(t => new SelectListItem() { Text = t.Name, Value = t.Name }).ToList();

                return View(viewModel);
            }

            var result = await IGDBService.Get(igdbid.Value, "genres.*", "game_modes.*", "multiplayer_modes.*", "release_dates.*", "platforms.*", "keywords.*", "involved_companies.*", "involved_companies.company.*", "cover.*");

            viewModel.Game = new Game()
            {
                IGDBId = result.Id.GetValueOrDefault(),
                Title = result.Name,
                Description = result.Summary,
                ReleasedOn = result.FirstReleaseDate.GetValueOrDefault().UtcDateTime,
                MultiplayerModes = new List<MultiplayerMode>(),
            };

            if (result.GameModes != null && result.GameModes.Values != null)
                viewModel.Game.Singleplayer = result.GameModes.Values.Any(gm => gm.Name == "Singleplayer");

            #region Multiplayer Modes
            if (result.MultiplayerModes != null && result.MultiplayerModes.Values != null)
            {
                var lan = result.MultiplayerModes.Values.Where(mm => mm.LanCoop.GetValueOrDefault()).OrderByDescending(mm => mm.OnlineMax).FirstOrDefault();
                var online = result.MultiplayerModes.Values.Where(mm => mm.OnlineCoop.GetValueOrDefault()).OrderByDescending(mm => mm.OnlineMax).FirstOrDefault();
                var offline = result.MultiplayerModes.Values.Where(mm => mm.OfflineCoop.GetValueOrDefault()).OrderByDescending(mm => mm.OnlineMax).FirstOrDefault();

                if (lan != null)
                {
                    viewModel.Game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.Lan,
                        MaxPlayers = lan.OnlineMax.GetValueOrDefault(),
                    });
                }

                if (online != null)
                {
                    viewModel.Game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.Online,
                        MaxPlayers = online.OnlineMax.GetValueOrDefault(),
                    });
                }

                if (offline != null)
                {
                    viewModel.Game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.Local,
                        MaxPlayers = offline.OfflineMax.GetValueOrDefault(),
                    });
                }
            }
            #endregion

            #region Publishers & Developers
            var companies = CompanyService.Get();

            if (result.InvolvedCompanies != null && result.InvolvedCompanies.Values != null)
            {
                // Make sure companie
                var developerNames = result.InvolvedCompanies.Values.Where(c => c.Developer.GetValueOrDefault()).Select(c => c.Company.Value.Name);
                var publisherNames = result.InvolvedCompanies.Values.Where(c => c.Publisher.GetValueOrDefault()).Select(c => c.Company.Value.Name);

                viewModel.Developers.AddRange(companies.Select(c => new SelectListItem()
                {
                    Text = c.Name,
                    Value = c.Name,
                    Selected = developerNames.Contains(c.Name),
                }));

                viewModel.Publishers.AddRange(companies.Select(c => new SelectListItem()
                {
                    Text = c.Name,
                    Value = c.Name,
                    Selected = publisherNames.Contains(c.Name),
                }));

                foreach (var developer in developerNames)
                {
                    if (!viewModel.Developers.Any(d => d.Value == developer))
                    {
                        viewModel.Developers.Add(new SelectListItem()
                        {
                            Text = developer,
                            Value = developer,
                            Selected = true
                        });
                    }
                }

                foreach (var publisher in publisherNames)
                {
                    if (!viewModel.Publishers.Any(d => d.Value == publisher))
                    {
                        viewModel.Publishers.Add(new SelectListItem()
                        {
                            Text = publisher,
                            Value = publisher,
                            Selected = true
                        });
                    }
                }

                viewModel.Developers = viewModel.Developers.OrderBy(d => d.Value).ToList();
                viewModel.Publishers = viewModel.Publishers.OrderBy(d => d.Value).ToList();
            }
            #endregion

            #region Genres
            var genres = GenreService.Get();

            if (result.Genres != null && result.Genres.Values != null)
            {
                var genreNames = result.Genres.Values.Select(g => g.Name);

                viewModel.Genres.AddRange(genres.Select(g => new SelectListItem()
                {
                    Text = g.Name,
                    Value = g.Name,
                    Selected = genreNames.Contains(g.Name),
                }));

                foreach (var genre in genreNames)
                {
                    if (!viewModel.Genres.Any(g => g.Value == genre))
                    {
                        viewModel.Genres.Add(new SelectListItem()
                        {
                            Text = genre,
                            Value = genre,
                            Selected = true
                        });
                    }
                }

                viewModel.Genres = viewModel.Genres.OrderBy(g => g.Value).ToList();
            }
            #endregion

            #region Tags
            var tags = TagService.Get();

            if (result.Keywords != null && result.Keywords.Values != null)
            {
                var tagNames = result.Keywords.Values.Select(t => t.Name).Take(20);

                viewModel.Tags.AddRange(genres.Select(t => new SelectListItem()
                {
                    Text = t.Name,
                    Value = t.Name,
                    Selected = tagNames.Contains(t.Name),
                }));

                foreach (var tag in tagNames)
                {
                    if (!viewModel.Tags.Any(t => t.Value == tag))
                    {
                        viewModel.Tags.Add(new SelectListItem()
                        {
                            Text = tag,
                            Value = tag,
                            Selected = true
                        });
                    }
                }
            }
            #endregion

            return View(viewModel);
        }

        // POST: Games/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(GameViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                await GameService.Add(viewModel.Game);

                return RedirectToAction(nameof(Index));
            }

            return View(viewModel.Game);
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

        [HttpPost]
        public async Task<IActionResult> Lookup(Game game)
        {
            var viewModel = new GameLookupResultsViewModel()
            {
                Search = game.Title
            };

            var results = await IGDBService.Search(game.Title, "involved_companies.*", "involved_companies.company.*");

            if (results == null)
                return View(new List<Game>());

            viewModel.Results = results.Select(r =>
            {
                return new Game()
                {
                    IGDBId = r.Id.GetValueOrDefault(),
                    Title = r.Name,
                    ReleasedOn = r.FirstReleaseDate.GetValueOrDefault().UtcDateTime,
                    Developers = r.InvolvedCompanies.Values.Where(c => c.Developer.HasValue && c.Developer.GetValueOrDefault() && c.Company != null && c.Company.Value != null).Select(c => new Company()
                    {
                        Name = c.Company.Value.Name
                    }).ToList()
                };
            });

            return View(viewModel);
        }

        /// <summary>
        /// Provides a list of possible games based on the given name
        /// </summary>
        /// <param name="name">Name of the game to lookup against IGDB</param>
        /// <returns></returns>
        public async Task<IActionResult> SearchMetadata(string name)
        {
            var metadata = await IGDBService.Search(name, "genres.*", "multiplayer_modes.*", "release_dates.*", "platforms.*", "keywords.*", "involved_companies.*", "involved_companies.company.*", "cover.*");

            if (metadata == null)
                return NotFound();

            return Json(metadata);
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
