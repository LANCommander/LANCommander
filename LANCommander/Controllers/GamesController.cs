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
using LANCommander.PCGamingWiki;

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
        private readonly PCGamingWikiClient PCGamingWikiClient;

        public GamesController(GameService gameService, ArchiveService archiveService, CategoryService categoryService, TagService tagService, GenreService genreService, CompanyService companyService, IGDBService igdbService)
        {
            GameService = gameService;
            ArchiveService = archiveService;
            CategoryService = categoryService;
            TagService = tagService;
            GenreService = genreService;
            CompanyService = companyService;
            IGDBService = igdbService;
            PCGamingWikiClient = new PCGamingWikiClient();
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
                viewModel.Game = new Game()
                {
                    Actions = new List<Data.Models.Action>(),
                    MultiplayerModes = new List<Data.Models.MultiplayerMode>()
                };

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
                Actions = new List<Data.Models.Action>(),
                MultiplayerModes = new List<MultiplayerMode>()
            };

            var playerCounts = await PCGamingWikiClient.GetMultiplayerPlayerCounts(result.Name);

            foreach (var playerCount in playerCounts)
            {
                MultiplayerType type;

                switch (playerCount.Key)
                {
                    case "Local Play":
                        type = MultiplayerType.Local;
                        break;

                    case "LAN Play":
                        type = MultiplayerType.Lan;
                        break;

                    case "Online Play":
                        type = MultiplayerType.Online;
                        break;

                    default:
                        continue;
                }

                viewModel.Game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    Type = type,
                    MaxPlayers = playerCount.Value,
                    MinPlayers = 2
                });
            }

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
                var game = await GameService.Add(viewModel.Game);

                if (viewModel.SelectedDevelopers != null && viewModel.SelectedDevelopers.Length > 0)
                    game.Developers = viewModel.SelectedDevelopers.Select(async d => await CompanyService.AddMissing(x => x.Name == d, new Company() { Name = d })).Select(t => t.Result).ToList();

                if (viewModel.SelectedPublishers != null && viewModel.SelectedPublishers.Length > 0)
                    game.Publishers = viewModel.SelectedPublishers.Select(async p => await CompanyService.AddMissing(x => x.Name == p, new Company() { Name = p })).Select(t => t.Result).ToList();

                if (viewModel.SelectedGenres != null && viewModel.SelectedGenres.Length > 0)
                    game.Genres = viewModel.SelectedGenres.Select(async g => await GenreService.AddMissing(x => x.Name == g, new Genre() { Name = g })).Select(t => t.Result).ToList();

                if (viewModel.SelectedTags != null && viewModel.SelectedTags.Length > 0)
                    game.Tags = viewModel.SelectedTags.Select(async t => await TagService.AddMissing(x => x.Name == t, new Tag() { Name = t })).Select(t => t.Result).ToList();

                await GameService.Update(game);

                return RedirectToAction(nameof(Index));
            }

            return View(viewModel.Game);
        }

        // GET: Games/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            var viewModel = new GameViewModel();

            viewModel.Game = await GameService.Get(id.GetValueOrDefault());

            if (viewModel.Game == null)
                return NotFound();

            viewModel.Developers = CompanyService.Get()
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem() { Text = c.Name, Value = c.Name, Selected = viewModel.Game.Developers.Any(d => d.Id == c.Id) })
                .ToList();

            viewModel.Publishers = CompanyService.Get()
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem() { Text = c.Name, Value = c.Name, Selected = viewModel.Game.Publishers.Any(d => d.Id == c.Id) })
                .ToList();

            viewModel.Genres = GenreService.Get()
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem() { Text = g.Name, Value = g.Name, Selected = viewModel.Game.Genres.Any(x => x.Id == g.Id) })
                .ToList();

            viewModel.Tags = TagService.Get()
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem() { Text = t.Name, Value = t.Name, Selected = viewModel.Game.Tags.Any(x => x.Id == t.Id) })
                .ToList();

            return View(viewModel);
        }

        // POST: Games/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, GameViewModel viewModel)
        {
            if (id != viewModel.Game.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var game = GameService.Get(g => g.Id == viewModel.Game.Id).FirstOrDefault();

                game.Title = viewModel.Game.Title;
                game.Description = viewModel.Game.Description;
                game.ReleasedOn = viewModel.Game.ReleasedOn;

                #region Update Developers
                if (viewModel.SelectedDevelopers == null)
                    viewModel.SelectedDevelopers = new string[0];

                foreach (var developer in game.Developers)
                {
                    if (!viewModel.SelectedDevelopers.Any(d => d == developer.Name))
                        game.Developers.Remove(developer);
                }

                foreach (var newDeveloper in viewModel.SelectedDevelopers.Where(sd => !game.Developers.Any(d => d.Name == sd)))
                {
                    game.Developers.Add(new Company()
                    {
                        Name = newDeveloper
                    });
                }
                #endregion

                #region Update Publishers
                if (viewModel.SelectedPublishers == null)
                    viewModel.SelectedPublishers = new string[0];

                foreach (var publisher in game.Publishers)
                {
                    if (!viewModel.SelectedPublishers.Any(p => p == publisher.Name))
                        game.Publishers.Remove(publisher);
                }

                foreach (var newPublisher in viewModel.SelectedPublishers.Where(sp => !game.Publishers.Any(p => p.Name == sp)))
                {
                    game.Publishers.Add(new Company()
                    {
                        Name = newPublisher
                    });
                }
                #endregion

                #region Update Genres
                if (viewModel.SelectedGenres == null)
                    viewModel.SelectedGenres = new string[0];

                foreach (var genre in game.Genres)
                {
                    if (!viewModel.SelectedGenres.Any(g => g == genre.Name))
                        game.Genres.Remove(genre);
                }

                foreach (var newGenre in viewModel.SelectedGenres.Where(sg => !game.Genres.Any(g => g.Name == sg)))
                {
                    game.Genres.Add(new Genre()
                    {
                        Name = newGenre
                    });
                }
                #endregion

                #region Update Tags
                if (viewModel.SelectedTags == null)
                    viewModel.SelectedTags = new string[0];

                foreach (var tag in game.Tags)
                {
                    if (!viewModel.SelectedTags.Any(t => t == tag.Name))
                        game.Tags.Remove(tag);
                }

                foreach (var newTag in viewModel.SelectedTags.Where(st => !game.Tags.Any(t => t.Name == st)))
                {
                    game.Tags.Add(new Tag()
                    {
                        Name = newTag
                    });
                }
                #endregion

                #region Update Actions
                if (game.Actions != null)
                {
                    game.Actions.Clear();

                    if (viewModel.Game.Actions != null)
                    {
                        foreach (var action in viewModel.Game.Actions)
                        {
                            game.Actions.Add(action);
                        }
                    }
                }
                #endregion

                #region Update MultiplayerModes
                if (game.MultiplayerModes != null)
                {
                    game.MultiplayerModes.Clear();

                    if (viewModel.Game.MultiplayerModes != null)
                    {
                        foreach (var multiplayerMode in viewModel.Game.MultiplayerModes)
                        {
                            game.MultiplayerModes.Add(multiplayerMode);
                        }
                    }
                }
                #endregion

                await GameService.Update(game);

                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
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
                var result = new Game()
                {
                    IGDBId = r.Id.GetValueOrDefault(),
                    Title = r.Name,
                    ReleasedOn = r.FirstReleaseDate.GetValueOrDefault().UtcDateTime,
                    Developers = new List<Company>()
                };

                if (r.InvolvedCompanies != null && r.InvolvedCompanies.Values != null)
                {
                    result.Developers = r.InvolvedCompanies.Values.Where(c => c.Developer.HasValue && c.Developer.GetValueOrDefault() && c.Company != null && c.Company.Value != null).Select(c => new Company()
                    {
                        Name = c.Company.Value.Name
                    }).ToList();
                }

                return result;
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
