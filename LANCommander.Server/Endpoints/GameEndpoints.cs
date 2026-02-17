using System.Net.Mime;
using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Games").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Manifest", GetManifestByIdAsync);
        group.MapGet("/{id:guid}/Actions", GetActionsByIdAsync);
        group.MapGet("/{id:guid}/Addons", GetAddonsByIdAsync);
        group.MapGet("/{id:guid}/Started", StartedAsync);
        group.MapGet("/{id:guid}/Stopped", StoppedAsync);
        group.MapGet("/{id:guid}/CheckForUpdate", CheckForUpdateAsync);
        group.MapGet("/{id:guid}/Download", DownloadAsync).AllowAnonymous();
        group.MapGet("/{id:guid}/Import", ImportAsync).RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapPost("/UploadArchive", UploadArchiveAsync).RequireAuthorization(RoleService.AdministratorRoleName);
    }

    internal static async Task<IResult> GetAsync(
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] LibraryService libraryService,
        [FromServices] IOptions<Settings.Settings> settings,
        [FromServices] IFusionCache cache,
        [FromServices] IMapper mapper,
        [FromServices] ILogger<Game> logger,
        ClaimsPrincipal userPrincipal)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? "");
        var userLibrary = await libraryService.GetByUserIdAsync(user.Id);

        var mappedGames = await cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>("Games", async _ =>
        {
            logger.LogDebug("Mapped games cache is empty, repopulating...");

            var games = await gameService
                .AsNoTracking()
                .AsSplitQuery()
                .GetAsync();

            return mapper.Map<IEnumerable<SDK.Models.Game>>(games);
        }, TimeSpan.MaxValue, tags: ["Games"]);

        foreach (var mappedGame in mappedGames)
        {
            if (userLibrary.Games != null)
                mappedGame.InLibrary = userLibrary.Games.Any(g => g.Id == mappedGame.Id);
        }
        
        if (settings.Value.Server.Roles.RestrictGamesByCollection && !userPrincipal.IsInRole(RoleService.AdministratorRoleName))
        {
            var roles = await userService.GetRolesAsync(user);

            var accessibleCollectionIds = roles.SelectMany(r => r.Collections.Select(c => c.Id)).Distinct();

            var accessibleGames = mappedGames.Where(g => g.Collections.Any(c => accessibleCollectionIds.Contains(c.Id)));

            foreach (var game in accessibleGames)
            {
                game.Collections = game.Collections.Where(c => accessibleCollectionIds.Contains(c.Id));
            }

            return TypedResults.Ok(accessibleGames);
        }
        
        return TypedResults.Ok(mappedGames);
    }

    internal static async Task<IResult> GetByIdAsync(
        [FromServices] GameService gameService,
        [FromServices] IFusionCache cache,
        [FromServices] IMapper mapper,
        Guid id)
    {
        var game = await cache.GetOrSetAsync<SDK.Models.Manifest.Game>($"Games/{id}", async _ =>
        {
            var result = await gameService
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
                .Query(q => q.Include(d => d.Redistributables).ThenInclude(r => r.Scripts))
                .Include(g => g.Scripts)
                .Include(g => g.Tags)
                .AsNoTracking()
                .AsSplitQuery()
                .GetAsync(id);
                
            return mapper.Map<SDK.Models.Manifest.Game>(result);
        }, TimeSpan.MaxValue, tags: ["Games", $"Games/{id}"]);

        if (game != null)
            return TypedResults.Ok(game);
        
        return TypedResults.NotFound();
    }

    internal static async Task<IResult> GetManifestByIdAsync(
        [FromServices] GameService gameService,
        [FromServices] IFusionCache cache,
        Guid id)
    {
        var manifest = await cache
            .GetOrSetAsync<SDK.Models.Manifest.Game>(
                $"Game/{id}/Manifest", 
                async _ => await gameService.GetManifestAsync(id),
                TimeSpan.MaxValue,
                tags: ["Games", $"Games/{id}"]);
        
        return TypedResults.Ok(manifest);
    }

    internal static async Task<IResult> GetActionsByIdAsync(
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] LibraryService libraryService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] IFusionCache cache,
        [FromServices] IMapper mapper,
        [FromServices] ILogger<Game> logger,
        ClaimsPrincipal userPrincipal,
        Guid id)
    {
        var actions = await cache.GetOrSetAsync<IEnumerable<SDK.Models.Action>>($"Games/{id}/Actions", async _ =>
        {
            var game = await gameService
                .Query(q =>
                {
                    return q
                        .Include(g => g.Actions)
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
                
            return mapper.Map<IEnumerable<SDK.Models.Action>>(actions);
        }, tags: ["Games", $"Games/{id}"]);
            
        return TypedResults.Ok(actions);
    }

    internal static async Task<IResult> GetAddonsByIdAsync(
        [FromServices] GameService gameService,
        [FromServices] IFusionCache cache,
        [FromServices] IMapper mapper,
        Guid id)
    {
        var addons = await cache.GetOrSetAsync($"Games/{id}/Addons", async _ =>
        {
            var results = await gameService
                .Include(g => g.Archives)
                .AsSplitQuery()
                .AsNoTracking()
                .GetAsync(g => g.BaseGameId == id && (g.Type == GameType.Expansion || g.Type == GameType.Mod));
                
            return mapper.Map<IEnumerable<SDK.Models.Game>>(results);
        }, tags: ["Games", $"Games/{id}"]);

        return TypedResults.Ok(addons);
    }

    internal static async Task<IResult> StartedAsync(
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] ServerService serverService,
        [FromServices] ILogger<Game> logger,
        ClaimsPrincipal userPrincipal,
        Guid id)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
        var game = await gameService.GetAsync(id);

        if (game == null || user == null)
            return TypedResults.BadRequest();

        #region Start recording play session
        var activeSessions = await playSessionService
            .Include(ps => ps.Game)
            .GetAsync(ps => ps.UserId == user.Id && ps.End == null);

        foreach (var activeSession in activeSessions)
            await playSessionService.EndSessionAsync(game.Id, activeSession.UserId);

        await playSessionService.StartSessionAsync(game.Id, user.Id);
        #endregion

        #region Autostart Servers
        await serverService.AutostartAsync(game.Id, ServerAutostartMethod.OnPlayerActivity);
        #endregion
            
        #region Run server scripts

        try
        {
            var servers = await serverService
                .GetAsync(s => s.GameId == game.Id);

            foreach (var server in servers)
            {
                await serverService.RunGameStartedScriptsAsync(server.Id, user.Id);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Server scripts could not run");
        }
        #endregion

        return TypedResults.Ok();
    }

    internal static async Task<IResult> StoppedAsync(
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] ServerService serverService,
        [FromServices] ILogger<Game> logger,
        ClaimsPrincipal userPrincipal,
        Guid id)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
        var game = await gameService.GetAsync(id);

        if (game == null || user == null)
            return TypedResults.BadRequest();
        
        await playSessionService.EndSessionAsync(game.Id, user.Id);
        
        #region Run server scripts
        try
        {
            var servers = await serverService
                .GetAsync(s => s.GameId == game.Id);

            foreach (var server in servers)
            {
                await serverService.RunGameStoppedScriptsAsync(server.Id, user.Id);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Server scripts could not run");
        }
        #endregion
        
        return TypedResults.Ok();
    }

    internal static async Task<IResult> CheckForUpdateAsync(
        [FromServices] GameService gameService,
        [FromServices] ILogger<Game> logger,
        Guid id,
        string version)
    {
        try
        {
            var currentVersion = await gameService.GetVersionAsync(id);

            return TypedResults.Ok(version != currentVersion);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Version could not be found for game {GameId}", id);
        }

        return TypedResults.Ok(false);
    }

    internal static async Task<IResult> DownloadAsync(
        [FromServices] GameService gameService,
        [FromServices] ArchiveService archiveService,
        [FromServices] IOptions<Settings.Settings> settings,
        [FromServices] ILogger<Game> logger,
        ClaimsPrincipal userPrincipal,
        Guid id)
    {
        if (!settings.Value.Server.Archives.AllowInsecureDownloads &&
            !(userPrincipal?.Identity?.IsAuthenticated ?? false))
        {
            logger.LogError("User is not authorized to download game with ID {GameId}", id);
            
            return TypedResults.Unauthorized();
        }
        
        var game = await gameService
            .Include(g => g.Archives)
            .GetAsync(id);

        if (game == null)
        {
            logger.LogError("Game with ID {GameId} could not be found", id);
            
            return TypedResults.NotFound();
        }

        if (!game.Archives.Any())
        {
            logger.LogError("No archives found for game with ID {GameId}", id);
            
            return TypedResults.NotFound();
        }

        var archive = await gameService.GetLatestArchiveAsync(id);
        var path = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(path))
        {
            logger?.LogError("No archive file exists for game with ID {GameId} at the expected path {Path}", id, path);
            
            return TypedResults.NotFound();
        }
        
        var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024, // 1 MB buffer for higher throughput on large archive downloads
            true);
        
        var contentType = MediaTypeNames.Application.Octet;
        var fileName = $"{game.Title.SanitizeFilename()}.zip";
        
        return TypedResults.File(fs, contentType, fileName);
    }

    internal static async Task<IResult> ImportAsync(
        [FromServices] ArchiveService archiveService,
        [FromServices] ImportContext importContext,
        [FromServices] ILogger<Game> logger,
        Guid objectKey)
    {
        try
        {
            var path = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

            var result = await importContext.InitializeImportAsync(path);

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to import game with object key {ObjectKey}", objectKey);
            
            return TypedResults.BadRequest(ex.Message);
        }
    }

    internal static async Task<IResult> UploadArchiveAsync(
        [FromServices] ArchiveService archiveService,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] ILogger<Game> logger,
        SDK.Models.UploadArchiveRequest request)
    {
        try
        {
            var storageLocation =
                await storageLocationService.GetOrDefaultAsync(request.StorageLocationId, StorageLocationType.Archive);
            
            var existingArchive = await archiveService.FirstOrDefaultAsync(a => a.GameId == request.Id && a.Version == request.Version);
            var existingArchivePath = await archiveService.GetArchiveFileLocationAsync(existingArchive);

            if (existingArchive == null)
            {
                existingArchive.ObjectKey = request.ObjectKey.ToString();
                existingArchive.Changelog = request.Changelog;
                existingArchive.StorageLocation = storageLocation;
                
                var uploadedArchivePath = await archiveService.GetArchiveFileLocationAsync(existingArchive);
                
                existingArchive.CompressedSize = new FileInfo(uploadedArchivePath).Length;

                await archiveService.UpdateAsync(existingArchive);
                
                File.Delete(existingArchivePath);
            }
            else
            {
                var archive = new Archive
                {
                    ObjectKey = request.ObjectKey.ToString(),
                    Changelog = request.Changelog,
                    GameId = request.Id,
                    StorageLocation = storageLocation
                };
                
                var uploadedArchivePath = await archiveService.GetArchiveFileLocationAsync(archive);

                archive.CompressedSize = new FileInfo(uploadedArchivePath).Length;

                await archiveService.AddAsync(archive);
            }

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not upload game archive");
            
            return TypedResults.BadRequest(ex.Message);
        }
    }
}