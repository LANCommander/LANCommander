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
        private readonly LibraryService LibraryService;
        private readonly UserService UserService;

        public DepotController(
            ILogger<DepotController> logger,
            IMapper mapper,
            IFusionCache cache,
            GameService gameService,
            LibraryService libraryService,
            UserService userService) : base(logger)
        {
            Mapper = mapper;
            Cache = cache;
            GameService = gameService;
            LibraryService = libraryService;
            UserService = userService;
        }

        [HttpGet("Games")]
        public async Task<IEnumerable<SDK.Models.DepotGame>> GetGamesAsync()
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var library = await LibraryService.GetByUserIdAsync(user.Id);

            var libraryGameIds = library.Games.Where(g => g != null).Select(g => g.Id).ToList();

            var games = await Cache.GetOrSetAsync("DepotGames", async _ =>
            {
                return await GameService
                    .Include(g => g.Media.Where(m => m.Type == SDK.Enums.MediaType.Cover))
                    .Include(g => g.Collections.Select(c => c.Id))
                    .Include(g => g.Developers.Select(c => c.Id))
                    .Include(g => g.Genres.Select(g => g.Id))
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms.Select(p => p.Id))
                    .Include(g => g.Publishers.Select(c => c.Id))
                    .Include(g => g.Tags.Select(t => t.Id))
                    .GetAsync<SDK.Models.DepotGame>();
            }, TimeSpan.MaxValue);

            var accessibleGames = games.ToList();

            if (Settings.Roles.RestrictGamesByCollection && !User.IsInRole(RoleService.AdministratorRoleName))
            {
                var roles = await UserService.GetRolesAsync(User?.Identity.Name);

                var accessibleCollectionIds = roles.SelectMany(r => r.Collections.Select(c => c.Id)).Distinct();

                accessibleGames = games.Where(g => g.Collections.Any(c => accessibleCollectionIds.Contains(c.Id))).ToList();

                foreach (var game in accessibleGames)
                {
                    game.Collections = game.Collections.Where(c => accessibleCollectionIds.Contains(c.Id));
                }
            }

            foreach (var game in accessibleGames)
            {
                if (libraryGameIds.Contains(game.Id))
                    game.InLibrary = true;
            }

            return accessibleGames;
        }

        [HttpGet("Games/{id}")]
        public async Task<SDK.Models.DepotGame> GetGameAsync(Guid id)
        {
            var game = await Cache.GetOrSetAsync($"DepotGames/{id}", async _ =>
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
