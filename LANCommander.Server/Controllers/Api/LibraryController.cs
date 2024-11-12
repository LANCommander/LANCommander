
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
        public async Task<IEnumerable<SDK.Models.Game>> Get()
        {
            try
            {
                var user = await UserService.Get(User.Identity.Name);
                var library = await LibraryService.GetByUserId(user.Id);
                var libraryGameIds = library.Games.Select(g => g.Id).ToList();

                return await Cache.GetOrSetAsync($"LibraryGames:{user.Id}", async _ =>
                {
                    return await GameService.Get<SDK.Models.Game>(g => libraryGameIds.Contains(g.Id));
                }, TimeSpan.MaxValue);
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        [HttpPost("AddToLibrary")]
        public async Task<bool> AddToLibrary(Guid gameId)
        {
            try
            {
                var user = await UserService.Get(User.Identity.Name);

                await LibraryService.AddToLibrary(user.Id, gameId);

                await Cache.ExpireAsync($"LibraryGames:{user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpPost("RemoveFromLibrary")]
        public async Task<bool> RemoveFromLibrary(Guid gameId)
        {
            try
            {
                var user = await UserService.Get(User.Identity.Name);

                await LibraryService.RemoveFromLibrary(user.Id, gameId);

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
