using AutoMapper;
using AutoMapper.QueryableExtensions;
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
        public async Task<IEnumerable<SDK.Models.DepotGame>> GetGames()
        {
            var user = await UserService.Get(User?.Identity?.Name);
            var library = await LibraryService.GetByUserId(user.Id);
            var libraryGameIds = library.Games.Select(g => g.Id).ToList();

            var games = await Cache.GetOrSetAsync("DepotGames", async _ =>
            {
                return await GameService.Get<SDK.Models.DepotGame>();
            }, TimeSpan.MaxValue);

            foreach (var game in games)
            {
                if (libraryGameIds.Contains(game.Id))
                    game.InLibrary = true;
            }

            return games;
        }

        [HttpGet("Games/{id}")]
        public async Task<SDK.Models.DepotGame> GetGame(Guid id)
        {
            var game = await GameService.Get(id);
            var user = await UserService.Get(User?.Identity?.Name);
            var library = await LibraryService.GetByUserId(user.Id);

            var result = Mapper.Map<SDK.Models.DepotGame>(game);

            if (library.Games.Any(g => g.Id == game.Id))
                result.InLibrary = true;

            return result;
        }
    }
}
