using System.IO.Compression;
using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Exceptions;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using System.Linq.Expressions;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services
{
    public class GameService(
        ILogger<GameService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        MediaService mediaService,
        StorageLocationService storageLocationService,
        SDK.Services.ScriptClient scriptClient) : BaseDatabaseService<Game>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Game> AddAsync(Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(g => g.Actions);
                await context.UpdateRelationshipAsync(g => g.Archives);
                await context.UpdateRelationshipAsync(g => g.BaseGame);
                await context.UpdateRelationshipAsync(g => g.Categories);
                await context.UpdateRelationshipAsync(g => g.Collections);
                await context.UpdateRelationshipAsync(g => g.CustomFields);
                await context.UpdateRelationshipAsync(g => g.DefaultArchive);
                await context.UpdateRelationshipAsync(g => g.Developers);
                await context.UpdateRelationshipAsync(g => g.Engine);
                await context.UpdateRelationshipAsync(g => g.Genres);
                await context.UpdateRelationshipAsync(g => g.Keys);
                await context.UpdateRelationshipAsync(g => g.Libraries);
                await context.UpdateRelationshipAsync(g => g.Media);
                await context.UpdateRelationshipAsync(g => g.MultiplayerModes);
                await context.UpdateRelationshipAsync(g => g.Pages);
                await context.UpdateRelationshipAsync(g => g.Platforms);
                await context.UpdateRelationshipAsync(g => g.Publishers);
                await context.UpdateRelationshipAsync(g => g.Redistributables);
                await context.UpdateRelationshipAsync(g => g.SavePaths);
                await context.UpdateRelationshipAsync(g => g.Scripts);
                await context.UpdateRelationshipAsync(g => g.Tags);
                await context.UpdateRelationshipAsync(g => g.ExternalIds);
            });
        }

        public override async Task<ExistingEntityResult<Game>> AddMissingAsync(Expression<Func<Game, bool>> predicate, Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<Game> UpdateAsync(Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            if (entity.Media != null)
                foreach (var media in entity.Media.Where(m => m.Id == Guid.Empty && String.IsNullOrWhiteSpace(m.Crc32)).ToList())
                    entity.Media.Remove(media);
            
            var update = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(g => g.Actions);
                await context.UpdateRelationshipAsync(g => g.Archives);
                await context.UpdateRelationshipAsync(g => g.BaseGame);
                await context.UpdateRelationshipAsync(g => g.Categories);
                await context.UpdateRelationshipAsync(g => g.Collections);
                await context.UpdateRelationshipAsync(g => g.CustomFields);
                await context.UpdateRelationshipAsync(g => g.DefaultArchive);
                await context.UpdateRelationshipAsync(g => g.Developers);
                await context.UpdateRelationshipAsync(g => g.Engine);
                await context.UpdateRelationshipAsync(g => g.Genres);
                await context.UpdateRelationshipAsync(g => g.Keys);
                await context.UpdateRelationshipAsync(g => g.Libraries);
                await context.UpdateRelationshipAsync(g => g.Media);
                await context.UpdateRelationshipAsync(g => g.MultiplayerModes);
                await context.UpdateRelationshipAsync(g => g.Pages);
                await context.UpdateRelationshipAsync(g => g.Platforms);
                await context.UpdateRelationshipAsync(g => g.Publishers);
                await context.UpdateRelationshipAsync(g => g.Redistributables);
                await context.UpdateRelationshipAsync(g => g.SavePaths);
                await context.UpdateRelationshipAsync(g => g.Scripts);
                await context.UpdateRelationshipAsync(g => g.Tags);
                await context.UpdateRelationshipAsync(g => g.ExternalIds);
            });

            return update;
        }

        public override async Task DeleteAsync(Game game)
        {
            game = await Include(
                g => g.Archives,
                g => g.Media)
                .GetAsync(game.Id);

            // Clear the explicit default-archive pointer before deleting the game's archives:
            // ArchiveService rejects deleting a game's current explicit default so bypassing the
            // admin UI can't leave that pointer dangling, but the whole Game row (and with it
            // DefaultArchiveId) is being removed here anyway, so clearing it first is a
            // deliberate, intentional "clear" rather than a bypass of that protection.
            if (game.DefaultArchiveId.HasValue)
                await SetDefaultArchiveAsync(game.Id, null);

            if (game.Archives != null)
                foreach (var archive in game.Archives.ToList())
                    await archiveService.DeleteAsync(archive);

            if (game.Media != null)
                foreach (var media in game.Media.ToList())
                    await mediaService.DeleteAsync(media);

            await cache.ExpireGameCacheAsync(game.Id);
            await base.DeleteAsync(game);
        }

        public async Task<ICollection<Game>> GetAddonsAsync(Game game)
        {
            return await GetAsync(g => g.AddonTypes.Contains(g.Type));
        }

        public async Task<SDK.Models.Manifest.Game> GetManifestAsync(Guid id)
        {
            var game = await GetManifestGameEntityAsync(id);

            return await GetManifestAsync(game);
        }

        /// <summary>
        /// Builds the game manifest for a specific archive rather than the effective default. The
        /// resulting manifest's <see cref="SDK.Models.Manifest.Game.Version"/> reflects the
        /// selected archive, not whichever archive would otherwise resolve as the default.
        /// </summary>
        /// <exception cref="ArchiveNotFoundForGameException">
        /// <paramref name="archiveId"/> does not identify an archive belonging to the game.
        /// </exception>
        public async Task<SDK.Models.Manifest.Game> GetManifestAsync(Guid id, Guid? archiveId)
        {
            if (!archiveId.HasValue)
                return await GetManifestAsync(id);

            var game = await GetManifestGameEntityAsync(id);

            if (game == null)
                return null;

            var archive = game.Archives?.FirstOrDefault(a => a.Id == archiveId.Value);

            if (archive == null)
                throw new ArchiveNotFoundForGameException(id, archiveId.Value);

            var manifest = await GetManifestAsync(game);
            manifest.Version = archive.Version;

            return manifest;
        }

        private async Task<Game> GetManifestGameEntityAsync(Guid id)
        {
            return await Query(q =>
            {
                return q
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(g => g.Actions)
                    .Include(g => g.Archives)
                    .Include(g => g.BaseGame)
                    .Include(g => g.Categories)
                    .Include(g => g.Collections)
                    .Include(g => g.CustomFields)
                    .Include(g => g.DependentGames)
                    .Include(g => g.Developers)
                    .Include(g => g.Engine)
                    .Include(g => g.Genres)
                    .Include(g => g.Media)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms)
                    .Include(g => g.Publishers)
                    .Include(g => g.Redistributables)
                    .Include(g => g.Tools)
                    .Include(g => g.SavePaths)
                    .Include(g => g.Scripts)
                    .Include(g => g.Tags)
                    .Include(g => g.ExternalIds);
            }).GetAsync(id);
        }
        
        public async Task<SDK.Models.Manifest.Game> GetManifestAsync(Game game)
        {
            if (game == null)
                return null;

            var manifest = mapper.Map<SDK.Models.Manifest.Game>(game);

            if (game.Redistributables != null && game.Redistributables.Any())
            {
                using var context = await contextFactory.CreateDbContextAsync();

                foreach (var redistributable in manifest.Redistributables)
                {
                    var joinEntry = await context.Set<Dictionary<string, object>>("GameRedistributable")
                        .FirstOrDefaultAsync(e =>
                            EF.Property<Guid>(e, "GameId") == game.Id &&
                            EF.Property<Guid>(e, "RedistributableId") == redistributable.Id);

                    if (joinEntry != null && joinEntry.TryGetValue("Options", out var options) && options is string optionsJson && !string.IsNullOrWhiteSpace(optionsJson))
                    {
                        redistributable.Options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson);
                    }
                }
            }

            return manifest;
        }

        public async Task<string> GetRedistributableOptionsAsync(Guid gameId, Guid redistributableId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var joinEntry = await context.Set<Dictionary<string, object>>("GameRedistributable")
                .FirstOrDefaultAsync(e =>
                    EF.Property<Guid>(e, "GameId") == gameId &&
                    EF.Property<Guid>(e, "RedistributableId") == redistributableId);

            if (joinEntry != null && joinEntry.TryGetValue("Options", out var options) && options is string optionsJson)
                return optionsJson;

            return null;
        }

        public async Task SetRedistributableOptionsAsync(Guid gameId, Guid redistributableId, string optionsJson)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var joinEntry = await context.Set<Dictionary<string, object>>("GameRedistributable")
                .FirstOrDefaultAsync(e =>
                    EF.Property<Guid>(e, "GameId") == gameId &&
                    EF.Property<Guid>(e, "RedistributableId") == redistributableId);

            if (joinEntry != null)
            {
                joinEntry["Options"] = optionsJson;
                await context.SaveChangesAsync();
            }
        }

        public async Task<GameCustomField> GetCustomFieldAsync(Guid id, string name)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.CustomFields)
                .GetAsync(id);
            
            return game.CustomFields.FirstOrDefault(c => c.Name == name);
        }

        public async Task<GameCustomField> SetCustomFieldAsync(Guid id, string name, string value)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.CustomFields)
                .GetAsync(id);
            
            if (game.CustomFields.Any(c => c.Name == name))
                foreach (var customField in game.CustomFields.Where(c => c.Name == name))
                    customField.Value = value;
            else
            {
                game.CustomFields.Add(new GameCustomField());
            }

            return await GetCustomFieldAsync(id, name);
        }

        /// <summary>
        /// Resolves a game's effective default archive: the explicit <see cref="Game.DefaultArchiveId"/>
        /// when it identifies an archive that belongs to this game, otherwise the newest archive by
        /// <see cref="Archive.CreatedOn"/>. Returns null when the game has no archives at all.
        /// </summary>
        /// <param name="game">A game with its <see cref="Game.Archives"/> collection loaded.</param>
        public static Archive? ResolveEffectiveDefaultArchive(Game game)
        {
            if (game?.Archives == null || !game.Archives.Any())
                return null;

            if (game.DefaultArchiveId.HasValue)
            {
                var explicitDefault = game.Archives.FirstOrDefault(a => a.Id == game.DefaultArchiveId.Value);

                if (explicitDefault != null)
                    return explicitDefault;
            }

            return game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
        }

        public async Task<Archive> GetLatestArchiveAsync(Guid id)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(id);

            return ResolveEffectiveDefaultArchive(game);
        }

        public async Task<string> GetVersionAsync(Guid id)
        {
            var latestArchive = await GetLatestArchiveAsync(id);

            return latestArchive?.Version ?? String.Empty;
        }

        /// <summary>
        /// Resolves a single archive server-side, either the explicitly requested
        /// <paramref name="archiveId"/> (validated to belong to the game) or, when omitted, the
        /// game's effective default. Callers such as install-plan generation must resolve exactly
        /// once through this method and record the returned archive's ID rather than re-deriving
        /// "latest" themselves later.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The game does not exist.</exception>
        /// <exception cref="ArchiveNotFoundForGameException">
        /// <paramref name="archiveId"/> was provided but does not identify an archive belonging to
        /// the game.
        /// </exception>
        public async Task<Archive> ResolveArchiveAsync(Guid gameId, Guid? archiveId)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(gameId);

            if (game == null)
                throw new KeyNotFoundException($"Game '{gameId}' was not found");

            if (archiveId.HasValue)
            {
                var requested = game.Archives?.FirstOrDefault(a => a.Id == archiveId.Value);

                if (requested == null)
                    throw new ArchiveNotFoundForGameException(gameId, archiveId.Value);

                return requested;
            }

            return ResolveEffectiveDefaultArchive(game);
        }

        /// <summary>
        /// Returns a game's selectable full archives (every stored archive is a complete,
        /// installable snapshot under the immutable-archive model) alongside the game entity so
        /// callers can compare against <see cref="Game.DefaultArchiveId"/> and the effective
        /// default. Returns a null <c>Game</c> when the game does not exist.
        /// </summary>
        public async Task<(Game? Game, Archive? EffectiveDefault)> GetSelectableArchivesAsync(Guid gameId)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(gameId);

            if (game == null)
                return (null, null);

            return (game, ResolveEffectiveDefaultArchive(game));
        }

        /// <summary>
        /// Sets (or clears) the game's explicit default archive. Validates that the archive belongs
        /// to the game before assigning it; passing null clears the explicit default so the effective
        /// default falls back to the newest archive by <c>CreatedOn</c>.
        /// </summary>
        /// <exception cref="InvalidDefaultArchiveException">
        /// Thrown when <paramref name="archiveId"/> does not identify an archive belonging to the game.
        /// </exception>
        public async Task<Game> SetDefaultArchiveAsync(Guid gameId, Guid? archiveId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var game = await context.Games
                .Include(g => g.Archives)
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game == null)
                throw new KeyNotFoundException($"Game '{gameId}' was not found");

            if (archiveId.HasValue && (game.Archives == null || game.Archives.All(a => a.Id != archiveId.Value)))
                throw new InvalidDefaultArchiveException(gameId, archiveId.Value);

            game.DefaultArchiveId = archiveId;
            game.UpdatedOn = DateTime.UtcNow;

            await context.SaveChangesAsync();

            await cache.ExpireGameCacheAsync(gameId);

            return game;
        }

        /// <summary>
        /// Returns archives newer than whatever is currently installed. When
        /// <paramref name="installedArchiveId"/> is supplied it takes precedence over
        /// <paramref name="version"/> for identifying the installed archive, since an exact archive
        /// ID is unambiguous while a version string could theoretically collide across archives;
        /// <paramref name="version"/> remains supported alone for existing callers that don't yet
        /// track the exact archive they installed.
        /// </summary>
        public async Task<IEnumerable<Archive>> GetUpdatesAsync(Guid gameId, string version, Guid? installedArchiveId = null)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(gameId);

            if (game?.Archives == null || !game.Archives.Any())
                return [];

            var orderedArchives = game.Archives.OrderBy(a => a.CreatedOn).ToList();

            Archive installedArchive = null;

            if (installedArchiveId.HasValue)
                installedArchive = orderedArchives.FirstOrDefault(a => a.Id == installedArchiveId.Value);

            if (installedArchive == null && string.IsNullOrWhiteSpace(version))
                return [orderedArchives.Last()];

            installedArchive ??= orderedArchives.FirstOrDefault(a => a.Version == version);

            if (installedArchive == null)
                return [orderedArchives.Last()];

            var newerArchives = orderedArchives
                .Where(a => a.CreatedOn > installedArchive.CreatedOn)
                .ToList();

            return newerArchives;
        }

        public async Task PackageAsync(Guid id)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .Include(g => g.CustomFields)
                .Include(g => g.Scripts)
                .GetAsync(id);
            
            logger.LogInformation("Packaging game {GameTitle}", game.Title);

            var latestArchive = ResolveEffectiveDefaultArchive(game);
            var storageLocation = await storageLocationService.GetOrDefaultAsync(latestArchive?.StorageLocationId, StorageLocationType.Archive);

            string? latestArchivePath = null;
            if (latestArchive != null)
                latestArchivePath = await archiveService.GetArchiveFileLocationAsync(latestArchive);

            if (game.Scripts?.Any(s => s.Type == ScriptType.Package) ?? false)
            {
                foreach (var script in game.Scripts.Where(s => s.Type == ScriptType.Package))
                {
                    logger.LogInformation("Running script {Name} for game {GameTitle}", script.Name, game.Title);
                    var package = await scriptClient.RunPackageScriptAsync(mapper.Map<SDK.Models.Script>(script), mapper.Map<SDK.Models.Game>(game), latestArchivePath);

                    if (package is null)
                    {
                        logger.LogError("Could not package game '{Title} ({Id})', the package script did not return a result", game.Title, game.Id);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(package.Path) || !Directory.Exists(package.Path))
                    {
                        logger.LogError("Could not package game '{Title} ({Id})', the path {Path} could not be found", game.Title, game.Id, package.Path);
                        return;
                    }

                    logger.LogInformation("New archive for game {GameTitle} will be created with version number {GameVersion}", game.Title, package.Version);

                    var archive = new Archive
                    {
                        Version = package.Version,
                        GameId = game.Id,
                        ObjectKey = Guid.NewGuid().ToString(),
                        LastVersion = latestArchive,
                        StorageLocationId = storageLocation.Id,
                    };

                    archive = await archiveService.AddAsync(archive);
                    
                    var destination = await archiveService.GetArchiveFileLocationAsync(archive);
                    
                    ZipFile.CreateFromDirectory(package.Path, destination);

                    await archiveService.RecalculateFileSizeArchiveAsync(archive);

                    logger.LogInformation("Successfully packaged {GameTitle} and created new archive with version number {GameVersion}", game.Title, archive.Version);
                }
            }
            else
            {
                logger.LogWarning("Could not package game '{GameTitle}', no packaging scripts are defined", game.Title);
                return;
            }
        }
    }
}
