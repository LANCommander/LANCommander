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
        private readonly DatabaseContext DatabaseContext;

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
            UserService userService,
            DatabaseContext databaseContext) : base(logger)
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
            DatabaseContext = databaseContext;
        }

        [HttpGet]
        public async Task<SDK.Models.DepotResults> GetAsync()
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var library = await LibraryService.GetByUserIdAsync(user.Id);

            var results = await Cache.GetOrSetAsync("Depot/Results", async _ =>
            {
                var results = new SDK.Models.DepotResults();

                results.Games = await GameService.GetAsync<DepotGame>();
                results.Collections = await CollectionService.GetAsync<SDK.Models.Collection>();
                results.Companies = await CompanyService.GetAsync<SDK.Models.Company>();
                results.Engines = await EngineService.GetAsync<SDK.Models.Engine>();
                results.Genres = await GenreService.GetAsync<SDK.Models.Genre>();
                results.Platforms = await PlatformService.GetAsync<SDK.Models.Platform>();
                results.Tags = await TagService.GetAsync<SDK.Models.Tag>();

                return results;
            }, TimeSpan.MaxValue);

            foreach (var game in results.Games)
            {
                game.InLibrary = library.Games.Any(g => g.Id == game.Id);
            }

            return results;
        }

        [HttpGet("Games/{id}")]
        public async Task<SDK.Models.DepotGame> GetGameAsync(Guid id)
        {
            var game = await Cache.GetOrSetAsync($"Depot/Games/{id}", async _ =>
            {
                return await GameService
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
            });

            var user = await UserService.GetAsync(User?.Identity?.Name);
            var library = await LibraryService.GetByUserIdAsync(user.Id);

            var result = Mapper.Map<SDK.Models.DepotGame>(game);

            if (library.Games.Any(g => g.Id == game.Id))
                result.InLibrary = true;

            return result;
        }
    }
}
