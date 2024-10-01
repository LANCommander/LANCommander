using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly GameService GameService;
        private readonly ArchiveService ArchiveService;
        private readonly UserManager<User> UserManager;
        private readonly RoleManager<Role> RoleManager;
        private readonly IFusionCache Cache;

        public GamesController(
            ILogger<GamesController> logger,
            IMapper mapper,
            IFusionCache cache,
            GameService gameService,
            ArchiveService archiveService,
            UserManager<User> userManager,
            RoleManager<Role> roleManager) : base(logger)
        {
            Mapper = mapper;
            GameService = gameService;
            ArchiveService = archiveService;
            UserManager = userManager;
            RoleManager = roleManager;
            Cache = cache;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Game>> Get()
        {
            var accessibleGames = new List<SDK.Models.Game>();
            var games = await GameService.Get(g => g.Type == SDK.Enums.GameType.MainGame || g.Type == SDK.Enums.GameType.StandaloneExpansion || g.Type == SDK.Enums.GameType.StandaloneMod).ToListAsync();

            var mappedGames = await Cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>("MappedGames", async _ => {
                Logger?.LogDebug("Mapped games cache is empty, repopulating");
                return Mapper.Map<IEnumerable<SDK.Models.Game>>(games);
            }, TimeSpan.FromHours(1));

            if (Settings.Roles.RestrictGamesByCollection && !User.IsInRole("Administrator"))
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);
                var roleNames = await UserManager.GetRolesAsync(user);
                var roles = new List<Role>();

                foreach (var name in roleNames)
                {
                    roles.Add(await RoleManager.FindByNameAsync(name));
                }

                var accessibleCollections = roles.SelectMany(r => r.Collections).DistinctBy(c => c.Id);
                var accessibleCollectionGames = accessibleCollections.SelectMany(c => c.Games).DistinctBy(g => g.Id).Select(g => g.Id);

                accessibleGames.AddRange(mappedGames.Where(mg => accessibleCollectionGames.Contains(mg.Id)));

                foreach (var game in accessibleGames)
                {
                    game.Collections = game.Collections.Where(c => accessibleCollections.Any(ac => ac.Id == c.Id));
                }
            }
            else
            {
                accessibleGames = mappedGames.ToList();
            }

            return accessibleGames;
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
            {
                Logger?.LogError("User is not authorized to download game with ID {GameId}", id);
                return Unauthorized();
            }

            var game = await GameService.Get(id);

            if (game == null)
            {
                Logger?.LogError("Game not found with ID {GameId}", id);
                return NotFound();
            }

            if (game.Archives == null || game.Archives.Count == 0)
            {
                Logger?.LogError("No archives found for game with ID {GameId}", id);
                return NotFound();
            }

            var archive = game.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
            {
                Logger?.LogError("No archive file exists for game with ID {GameId} at the expected path {FileName}", id, filename);
                return NotFound();
            }

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Title.SanitizeFilename()}.zip");
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost("Import/{objectKey}")]
        public async Task<IActionResult> Import(Guid objectKey)
        {
            try
            {
                var game = await GameService.Import(objectKey);

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not import game from upload");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost("UploadArchive")]
        public async Task<IActionResult> UploadArchive(SDK.Models.UploadArchiveRequest request)
        {
            try
            {
                var archive = await ArchiveService.Get(a => a.GameId == request.Id && a.Version == request.Version).FirstOrDefaultAsync();
                var archivePath = ArchiveService.GetArchiveFileLocation(archive.ObjectKey);

                if (archive != null)
                {
                    var existingArchivePath = ArchiveService.GetArchiveFileLocation(archive.ObjectKey);

                    System.IO.File.Delete(existingArchivePath);

                    archive.ObjectKey = request.ObjectKey.ToString();
                    archive.Changelog = request.Changelog;
                    archive.CompressedSize = new System.IO.FileInfo(archivePath).Length;

                    archive = await ArchiveService.Update(archive);
                }
                else
                {
                    archive = new Archive()
                    {
                        ObjectKey = request.ObjectKey.ToString(),
                        Changelog = request.Changelog,
                        GameId = request.Id,
                        CompressedSize = new System.IO.FileInfo(archivePath).Length,
                    };

                    await ArchiveService.Add(archive);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not upload game archive");
                return BadRequest(ex.Message);
            }
        }
    }
}
