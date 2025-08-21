using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.SDK.Extensions;
using LANCommander.Server.Data;
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
        private readonly DatabaseContext DatabaseContext;

        public LibraryController(
            ILogger<LibraryController> logger,
            IMapper mapper,
            IFusionCache cache,
            GameService gameService,
            LibraryService libraryService,
            DatabaseContext databaseContext,
            UserService userService) : base(logger)
        {
            Mapper = mapper;
            Cache = cache;
            GameService = gameService;
            LibraryService = libraryService;
            UserService = userService;
            DatabaseContext = databaseContext;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.EntityReference>> GetAsync()
        {
            try
            {
                if (!Settings.Library.EnableUserLibraries)
                {
                    var games = await Cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>("Games", async _ => {
                        Logger?.LogDebug("Mapped games cache is empty, repopulating");
                        
                        var games = await GameService
                            .AsNoTracking()
                            .AsSplitQuery()
                            .GetAsync();

                        return Mapper.Map<IEnumerable<SDK.Models.Game>>(games);
                    }, TimeSpan.MaxValue, tags: ["Games"]);
                    
                    return games.OrderByTitle(g => String.IsNullOrWhiteSpace(g.SortTitle) ? g.Title : g.SortTitle).Select(g => Mapper.Map<SDK.Models.EntityReference>(g));
                }
                
                var user = await UserService.GetAsync(User.Identity.Name);
                
                return await Cache.GetOrSetAsync($"Library/{user.Id}", async _ =>
                {
                    var library = await LibraryService.GetByUserIdAsync(user.Id);
                    var libraryGameIds = library.Games.Select(g => g.Id).ToList();
                    
                    var games = await DatabaseContext.Games
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Where(g => libraryGameIds.Contains(g.Id))
                        .ToListAsync();
                    
                    return Mapper.Map<IEnumerable<SDK.Models.EntityReference>>(games.OrderByTitle(g => String.IsNullOrWhiteSpace(g.SortTitle) ? g.Title : g.SortTitle));
                }, TimeSpan.MaxValue, tags: ["Library"]);
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        [HttpPost("AddToLibrary/{id}")]
        public async Task<bool> AddToLibraryAsync(Guid id)
        {
            try
            {
                var user = await UserService.GetAsync(User.Identity.Name);

                await LibraryService.AddToLibraryAsync(user.Id, id);

                await Cache.ExpireAsync($"Library/{user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpPost("RemoveFromLibrary/{id}")]
        public Task<bool> RemoveFromLibraryAsync(Guid id)
        {
            return RemoveFromLibraryAsync(id, SDK.Models.GenericGuidsRequest.Empty);
        }

        [HttpPost("RemoveFromLibrary/{id}/addons")]
        public async Task<bool> RemoveFromLibraryAsync(Guid id, [FromBody] SDK.Models.GenericGuidsRequest addonGuids)
        {
            try
            {
                var user = await UserService.GetAsync(User.Identity.Name);

                await LibraryService.RemoveFromLibraryAsync(user.Id, id, addonGuids?.Guids ?? []);

                await Cache.ExpireAsync($"Library/{user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
