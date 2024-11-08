
using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly LibraryService LibraryService;
        private readonly UserService UserService;

        public LibraryController(
            ILogger<LibraryController> logger,
            IMapper mapper,
            IFusionCache cache,
            LibraryService libraryService,
            UserService userService) : base(logger)
        {
            Mapper = mapper;
            Cache = cache;
            LibraryService = libraryService;
            UserService = userService;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Game>> Get()
        {
            try
            {
                var user = await UserService.Get(User.Identity.Name);

                return await Cache.GetOrSetAsync($"LibraryGames:{user.Id}", async _ =>
                {
                    return Mapper.Map<IEnumerable<SDK.Models.Game>>(await LibraryService.Get(user.Id));
                }, TimeSpan.FromDays(1));
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
