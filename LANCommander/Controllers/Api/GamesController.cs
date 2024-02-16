using AutoMapper;
using LANCommander.Data;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly IMapper Mapper;
        private readonly GameService GameService;
        private readonly UserManager<User> UserManager;
        private readonly RoleManager<Role> RoleManager;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public GamesController(IMapper mapper, GameService gameService, UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            Mapper = mapper;
            GameService = gameService;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.GameManifest>> Get()
        {
            var games = new List<Game>();

            if (Settings.Roles.RestrictGamesByCollection && !User.IsInRole("Administrator"))
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);
                var roles = await UserManager.GetRolesAsync(user);

                foreach (var roleName in roles)
                {
                    var role = await RoleManager.FindByNameAsync(roleName);

                    games.AddRange(role.Collections.SelectMany(c => c.Games).DistinctBy(g => g.Id).ToList());
                }

                games = games.Where(g => g.Type == GameType.MainGame || g.Type == GameType.StandaloneExpansion || g.Type == GameType.StandaloneMod).DistinctBy(g => g.Id).ToList();
            }
            else
            {
                games = await GameService.Get(g => g.Type == GameType.MainGame || g.Type == GameType.StandaloneExpansion || g.Type == GameType.StandaloneMod).ToListAsync();
            }

            return Mapper.Map<IEnumerable<SDK.GameManifest>>(games.Select(g => GameService.GetManifest(g)));
        }

        [HttpGet("{id}")]
        public async Task<SDK.Models.Game> Get(Guid id)
        {
            return Mapper.Map<SDK.Models.Game>(await GameService.Get(id));
        }

        [HttpGet("{id}/Manifest")]
        public async Task<SDK.GameManifest> GetManifest(Guid id)
        {
            var manifest = await GameService.GetManifest(id);

            return manifest;
        }

        [AllowAnonymous]
        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            if (!Settings.Archives.AllowInsecureDownloads && (User == null || User.Identity == null || !User.Identity.IsAuthenticated))
                return Unauthorized();

            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            if (game.Archives == null || game.Archives.Count == 0)
                return NotFound();

            var archive = game.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Title.SanitizeFilename()}.zip");
        }
    }
}
