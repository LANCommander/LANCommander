using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.ImportExport;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
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
        private readonly GameService GameService;
        private readonly LibraryService LibraryService;
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveService ArchiveService;
        private readonly UserService UserService;
        private readonly PlaySessionService PlaySessionService;
        private readonly ServerService ServerService;
        private readonly IEnumerable<IServerEngine> ServerEngines;
        private readonly ImportContext ImportContext;
        private readonly IFusionCache Cache;
        private readonly IMapper Mapper;

        public GamesController(
            ILogger<GamesController> logger,
            IServiceProvider serviceProvider,
            IFusionCache cache,
            IMapper mapper,
            GameService gameService,
            LibraryService libraryService,
            StorageLocationService storageLocationService,
            ArchiveService archiveService,
            UserService userService,
            PlaySessionService playSessionService,
            ServerService serverService,
            ImportContext importContext) : base(logger)
        {
            GameService = gameService;

            LibraryService = libraryService;
            StorageLocationService = storageLocationService;
            ArchiveService = archiveService;
            UserService = userService;
            PlaySessionService = playSessionService;
            ServerService = serverService;
            ServerEngines = serviceProvider.GetServices<IServerEngine>();
            ImportContext = importContext;
            Cache = cache;
            Mapper = mapper;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Game>> GetAsync()
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var userLibrary = await LibraryService.GetByUserIdAsync(user.Id);

            var mappedGames = await Cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>("Games", async _ => {
                Logger?.LogDebug("Mapped games cache is empty, repopulating");

                var games = await GameService
                    .AsNoTracking()
                    .AsSplitQuery()
                    .GetAsync<SDK.Models.Game>();

                return games;
            }, TimeSpan.MaxValue, tags: ["Games"]);

            foreach (var mappedGame in mappedGames)
            {
                if (userLibrary.Games != null)
                    mappedGame.InLibrary = userLibrary.Games.Any(g => g.Id == mappedGame.Id);
            }

            if (Settings.Roles.RestrictGamesByCollection && !User.IsInRole(RoleService.AdministratorRoleName))
            {
                var roles = await UserService.GetRolesAsync(user);

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
            var game = await Cache.GetOrSetAsync<SDK.Models.Game>($"Games/{id}", async _ =>
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
                    .Include(g => g.Scripts)
                    .Include(g => g.Tags)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .GetAsync<SDK.Models.Game>(id);
            }, TimeSpan.MaxValue, tags: ["Games", $"Games/{id}"]);

            return game;
        }

        [HttpGet("{id}/Manifest")]
        public async Task<SDK.GameManifest> GetManifest(Guid id)
        {
            var manifest = await Cache.GetOrSetAsync($"Games/{id}/Manifest", async _ =>
            {
                return await GameService.GetManifestAsync(id);
            }, tags: ["Games", $"Games/{id}"]);

            return manifest;
        }

        [HttpGet("{id}/Actions")]
        public async Task<IEnumerable<SDK.Models.Action>> GetActionsAsync(Guid id)
        {
            var actions = await Cache.GetOrSetAsync<IEnumerable<SDK.Models.Action>>($"Games/{id}/Actions", async _ =>
            {
                var game = await GameService
                    .Query(q =>
                    {
                        return q.Include(g => g.Actions)
                            .Include(g => g.DependentGames)
                            .ThenInclude(dg => dg.Actions)
                            .Include(g => g.Servers)
                            .ThenInclude(s => s.Actions);
                    })
                    .AsNoTracking()
                    .AsSplitQuery()
                    .GetAsync(id);

                var actions = new List<Data.Models.Action>();
                
                actions.AddRange(game.Actions.OrderBy(a => a.SortOrder));
                actions.AddRange(game.DependentGames.Where(dg => dg.Type == GameType.Expansion || dg.Type == GameType.Mod).OrderBy(dg => String.IsNullOrWhiteSpace(dg.SortTitle) ? dg.Title : dg.SortTitle).SelectMany(dg => dg.Actions.OrderBy(a => a.SortOrder)));
                actions.AddRange(game.Servers.SelectMany(s => s.Actions));
                
                return Mapper.Map<IEnumerable<SDK.Models.Action>>(actions);
            }, tags: ["Games", $"Games/{id}"]);
            
            return actions;
        }

        [HttpGet("{id}/Addons")]
        public async Task<IEnumerable<SDK.Models.Game>> GetAddonsAsync(Guid id)
        {
            var addons = await Cache.GetOrSetAsync($"Games/{id}/Addons", async _ =>
            {
                return await GameService
                    .Include(g => g.Archives)
                    .AsSplitQuery()
                    .AsNoTracking()
                    .GetAsync<SDK.Models.Game>(g => g.BaseGameId == id && (g.Type == GameType.Expansion || g.Type == GameType.Mod));
            }, tags: ["Games", $"Games/{id}"]);

            return addons;
        }

        [HttpGet("{id}/Started")]
        public async Task<IActionResult> StartedAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var game = await GameService.GetAsync(id);

            if (game == null || user == null)
                return BadRequest();

            #region Start recording play session
            var activeSessions = await PlaySessionService
                .Include(ps => ps.Game)
                .GetAsync(ps => ps.UserId == user.Id && ps.End == null);

            foreach (var activeSession in activeSessions)
                await PlaySessionService.EndSessionAsync(game.Id, activeSession.UserId);

            await PlaySessionService.StartSessionAsync(game.Id, user.Id);
            #endregion

            #region Autostart Servers
            try
            {
                var servers = await ServerService.GetAsync(s =>
                    s.GameId == game.Id && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

                foreach (var serverEngine in ServerEngines)
                {
                    foreach (var server in servers)
                    {
                        if (serverEngine.IsManaging(server.Id) && (await serverEngine.GetStatusAsync(server.Id) == ServerProcessStatus.Stopped))
                            await serverEngine.StartAsync(server.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Servers could not be autostarted");
            }
            #endregion
            
            #region Run server scripts

            try
            {
                var servers = await ServerService
                    .GetAsync(s => s.GameId == game.Id);

                foreach (var server in servers)
                {
                    await ServerService.RunGameStartedScriptsAsync(server.Id, user.Id);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Server scripts could not run");
            }
            #endregion

            return Ok();
        }

        [HttpGet("{id}/Stopped")]
        public async Task<IActionResult> StoppedAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var game = await GameService.GetAsync(id);

            if (game == null || user == null)
                return BadRequest();

            await PlaySessionService.EndSessionAsync(game.Id, user.Id);
            
            #region Run server scripts
            try
            {
                var servers = await ServerService
                    .GetAsync(s => s.GameId == game.Id);

                foreach (var server in servers)
                {
                    await ServerService.RunGameStoppedScriptsAsync(server.Id, user.Id);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Server scripts could not run");
            }
            #endregion

            return Ok();
        }

        [HttpGet("{id}/CheckForUpdate")]
        public async Task<bool> CheckForUpdateAsync(Guid id, string version)
        {
            var gameArchives = await ArchiveService.GetAsync(a => a.GameId == id);

            var latestArchive = gameArchives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

            if (latestArchive?.Version != version)
                return true;
            else
                return false;
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

            var game = await GameService
                .Include(g => g.Archives)
                .GetAsync(id);

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

            var filename = await ArchiveService.GetArchiveFileLocationAsync(archive);

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
                var uploadedPath = await ArchiveService.GetArchiveFileLocationAsync(objectKey.ToString());

                var result = await ImportContext.InitializeImportAsync(uploadedPath);

                return Ok(result);
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
                var archivePath = await ArchiveService.GetArchiveFileLocationAsync(archive);

                if (archive != null)
                {
                    var existingArchivePath = await ArchiveService.GetArchiveFileLocationAsync(archive);

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
