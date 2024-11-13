
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly IFusionCache Cache;
        private readonly GameService GameService;
        private readonly LibraryService LibraryService;
        private readonly UserService UserService;

        public LibraryController(
            ILogger<LibraryController> logger,
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

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Game>> GetAsync()
        {
            try
            {
                var user = await UserService.GetAsync(User.Identity.Name);
                var roles = await UserService.GetRolesAsync(User?.Identity.Name);
                var library = await LibraryService.GetByUserIdAsync(user.Id);
                var libraryGameIds = library.Games.Select(g => g.Id).ToList();

                var accessibleCollectionIds = roles.SelectMany(r => r.Collections.Select(c => c.Id)).Distinct();

                return await Cache.GetOrSetAsync($"LibraryGames:{user.Id}", async _ =>
                {
                    var games = await GameService.GetAsync<SDK.Models.Game>(g => libraryGameIds.Contains(g.Id));

                    foreach (var game in games)
                    {
                        game.PlaySessions = game.PlaySessions.Where(ps => ps.UserId == user.Id);
                        game.Collections = game.Collections.Where(c => accessibleCollectionIds.Contains(c.Id));
                        game.InLibrary = true;
                    }

                    return games;
                }, TimeSpan.MaxValue);
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        [HttpPost("AddToLibrary")]
        public async Task<bool> AddToLibraryAsync(Guid gameId)
        {
            try
            {
                var user = await UserService.GetAsync(User.Identity.Name);

                await LibraryService.AddToLibraryAsync(user.Id, gameId);

                await Cache.ExpireAsync($"LibraryGames:{user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpPost("RemoveFromLibrary")]
        public async Task<bool> RemoveFromLibraryAsync(Guid gameId)
        {
            try
            {
                var user = await UserService.GetAsync(User.Identity.Name);

                await LibraryService.RemoveFromLibraryAsync(user.Id, gameId);

                await Cache.ExpireAsync($"LibraryGames:{user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
