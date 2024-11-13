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
        private readonly LibraryService LibraryService;
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveService ArchiveService;
        private readonly UserService UserService;
        private readonly RoleService RoleService;
        private readonly IFusionCache Cache;

        public GamesController(
            ILogger<GamesController> logger,
            IMapper mapper,
            IFusionCache cache,
            GameService gameService,
            LibraryService libraryService,
            StorageLocationService storageLocationService,
            ArchiveService archiveService,
            UserService userService,
            RoleService roleService) : base(logger)
        {
            Mapper = mapper;
            GameService = gameService;
            LibraryService = libraryService;
            StorageLocationService = storageLocationService;
            ArchiveService = archiveService;
            UserService = userService;
            RoleService = roleService;
            Cache = cache;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Game>> GetAsync()
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var userLibrary = await LibraryService.GetByUserIdAsync(user.Id);

            var mappedGames = await Cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>("MappedGames", async _ => {
                Logger?.LogDebug("Mapped games cache is empty, repopulating");

                var games = await GameService.GetAsync<SDK.Models.Game>();

                return games;
            }, TimeSpan.MaxValue);

            foreach (var mappedGame in mappedGames)
            {
                mappedGame.PlaySessions = mappedGame.PlaySessions.Where(ps => ps.UserId == user.Id);

                if (userLibrary.Games != null)
                    mappedGame.InLibrary = userLibrary.Games.Any(g => g.Id == mappedGame.Id);
            }

            if (Settings.Roles.RestrictGamesByCollection && !User.IsInRole(RoleService.AdministratorRoleName))
            {
                var roles = await UserService.GetRolesAsync(User?.Identity.Name);

                var accessibleCollectionIds = roles.SelectMany(r => r.Collections.Select(c => c.Id)).Distinct();

                var accessibleGames = mappedGames.Where(g => g.Collections.Any(c => accessibleCollectionIds.Contains(c.Id)));

                foreach (var game in accessibleGames)
                {
                    game.Collections = game.Collections.Where(c => accessibleCollectionIds.Contains(c.Id));
                }

                return accessibleGames;
            }
            else
            {
                return mappedGames;
            }
        }

        [HttpGet("{id}")]
        public async Task<SDK.Models.Game> GetAsync(Guid id)
        {
            return Mapper.Map<SDK.Models.Game>(await GameService.GetAsync(id));
        }

        [HttpGet("{id}/Manifest")]
        public async Task<SDK.GameManifest> GetManifest(Guid id)
        {
            var manifest = await GameService.GetManifestAsync(id);

            return manifest;
        }

        [AllowAnonymous]
        [HttpGet("{id}/Download")]
        public async Task<IActionResult> DownloadAsync(Guid id)
        {
            if (!Settings.Archives.AllowInsecureDownloads && (User == null || User.Identity == null || !User.Identity.IsAuthenticated))
            {
                Logger?.LogError("User is not authorized to download game with ID {GameId}", id);
                return Unauthorized();
            }

            var game = await GameService.GetAsync(id);

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

            var filename = ArchiveService.GetArchiveFileLocation(archive);

            if (!System.IO.File.Exists(filename))
            {
                Logger?.LogError("No archive file exists for game with ID {GameId} at the expected path {FileName}", id, filename);
                return NotFound();
            }

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Title.SanitizeFilename()}.zip");
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("Import/{objectKey}")]
        public async Task<IActionResult> ImportAsync(Guid objectKey)
        {
            try
            {
                var game = await GameService.ImportAsync(objectKey);

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not import game from upload");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("UploadArchive")]
        public async Task<IActionResult> UploadArchiveAsync(SDK.Models.UploadArchiveRequest request)
        {
            try
            {
                var storageLocation = await StorageLocationService.FirstOrDefaultAsync(l => request.StorageLocationId.HasValue ? l.Id == request.StorageLocationId.Value : l.Default);
                var archive = await ArchiveService.FirstOrDefaultAsync(a => a.GameId == request.Id && a.Version == request.Version);
                var archivePath = ArchiveService.GetArchiveFileLocation(archive);

                if (archive != null)
                {
                    var existingArchivePath = ArchiveService.GetArchiveFileLocation(archive);

                    System.IO.File.Delete(existingArchivePath);

                    archive.ObjectKey = request.ObjectKey.ToString();
                    archive.Changelog = request.Changelog;
                    archive.CompressedSize = new System.IO.FileInfo(archivePath).Length;
                    archive.StorageLocation = storageLocation;

                    archive = await ArchiveService.UpdateAsync(archive);
                }
                else
                {
                    archive = new Archive()
                    {
                        ObjectKey = request.ObjectKey.ToString(),
                        Changelog = request.Changelog,
                        GameId = request.Id,
                        CompressedSize = new System.IO.FileInfo(archivePath).Length,
                        StorageLocation = storageLocation
                    };

                    await ArchiveService.AddAsync(archive);
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
