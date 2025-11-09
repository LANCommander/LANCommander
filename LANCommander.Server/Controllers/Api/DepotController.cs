using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class DepotController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly IFusionCache Cache;
        private readonly GameService GameService;
        private readonly CollectionService CollectionService;
        private readonly CompanyService CompanyService;
        private readonly EngineService EngineService;
        private readonly GenreService GenreService;
        private readonly PlatformService PlatformService;
        private readonly TagService TagService;
        private readonly LibraryService LibraryService;
        private readonly UserService UserService;

        public DepotController(
            ILogger<DepotController> logger,
            IMapper mapper,
            IFusionCache cache,
            GameService gameService,
            CollectionService collectionService,
            CompanyService companyService,
            EngineService engineService,
            GenreService genreService,
            PlatformService platformService,
            TagService tagService,
            LibraryService libraryService,
            UserService userService) : base(logger)
        {
            Mapper = mapper;
            Cache = cache;
            GameService = gameService;
            CollectionService = collectionService;
            CompanyService = companyService;
            EngineService = engineService;
            GenreService = genreService;
            PlatformService = platformService;
            TagService = tagService;
            LibraryService = libraryService;
            UserService = userService;
        }

        [HttpGet]
        public async Task<SDK.Models.DepotResults> GetAsync()
        {
            var userName = User?.Identity?.Name;
            var user = await UserService
                .Query(q =>
                {
                    return q
                        .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role);
                })
                .FirstOrDefaultAsync(u => u.UserName.ToUpper() == userName.ToUpper());
            
            if (user == null)
                return new SDK.Models.DepotResults();
            
            var library = await LibraryService.GetByUserIdAsync(user.Id);

            var results = await Cache.GetOrSetAsync("Depot/Results", async _ =>
            {
                var games = await GameService
                    .AsNoTracking()
                    .Include(g => g.Media)
                    .Include(g => g.Collections)
                    .Include(g => g.Platforms)
                    .Include(g => g.Tags)
                    .Include(g => g.Developers)
                    .Include(g => g.Genres)
                    .Include(g => g.Publishers)
                    .Include(g => g.Engine)
                    .GetAsync();
                
                var results = new SDK.Models.DepotResults()
                {
                    Games = Mapper.Map<ICollection<DepotGame>>(games),
                    Collections = Mapper.Map<ICollection<SDK.Models.Collection>>(await CollectionService.AsNoTracking().GetAsync()),
                    Companies = Mapper.Map<ICollection<SDK.Models.Company>>(await CompanyService.AsNoTracking().GetAsync()),
                    Engines = Mapper.Map<ICollection<SDK.Models.Engine>>(await EngineService.AsNoTracking().GetAsync()),
                    Genres = Mapper.Map<ICollection<SDK.Models.Genre>>(await GenreService.AsNoTracking().GetAsync()),
                    Platforms = Mapper.Map<ICollection<SDK.Models.Platform>>(await PlatformService.AsNoTracking().GetAsync()),
                    Tags = Mapper.Map<ICollection<SDK.Models.Tag>>(await TagService.AsNoTracking().GetAsync()),
                };

                return results;
            }, TimeSpan.MaxValue, tags: ["Depot"]);

            if (Settings.Roles.RestrictGamesByCollection)
            {
                var collections = await UserService.GetCollectionsAsync(user);

                results.Games = results
                    .Games
                    .Where(g =>
                        g.Collections.Any(gc =>
                            collections.Any(c => c.Id == gc.Id)))
                    .ToList();
            }

            foreach (var game in results.Games)
            {
                game.InLibrary = library.Games.Any(g => g.Id == game.Id);
            }

            if (!results.Games.Any())
            {
                string roles = "No roles";

                if (user?.Roles?.Any() ?? false)
                    String.Join(", ", user.Roles.Select(r => r.Name));
                
                Logger?.LogInformation("No games found in depot for user {UserName} (Roles: {Roles})", user?.UserName, roles);
            }

            return results;
        }

        [HttpGet("Games/{id}")]
        public async Task<SDK.Models.DepotGame> GetGameAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            
            var game = await Cache.GetOrSetAsync($"Depot/Games/{id}", async _ =>
            {
                return await GameService.Query(q =>
                    {
                        return q.AsNoTracking();
                    })
                    .Include(g => g.Actions)
                    .Include(g => g.Archives)
                    .Include(g => g.BaseGame)
                    .Include(g => g.Categories)
                    .Include(g => g.Collections)
                    .Include(g => g.DependentGames)
                    .Include(g => g.Developers)
                    .Include(g => g.Engine)
                    .Include(g => g.Genres)
                    .Include(g => g.Media)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms)
                    .Include(g => g.Publishers)
                    .Include(g => g.Redistributables)
                    .Include(g => g.SavePaths)
                    .Include(g => g.Scripts)
                    .Include(g => g.Tags)
                    .GetAsync(id);
            }, tags: ["Depot", "Depot/Games", "Games", $"Games/{id}"]);

            var library = await LibraryService
                .AsNoTracking()
                .Include(l => l.Games)
                .FirstOrDefaultAsync(l => l.UserId == user.Id);

            var result = Mapper.Map<SDK.Models.DepotGame>(game);

            if (library.Games.Any(g => g.Id == game.Id))
                result.InLibrary = true;

            return result;
        }
    }
}
