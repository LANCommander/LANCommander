using System.IO.Compression;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Mappers;
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
        SdkMapper sdkMapper,
        ManifestMapper manifestMapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        GameVersionService gameVersionService,
        MediaService mediaService,
        StorageLocationService storageLocationService,
        GameCustomFieldService gameCustomFieldService,
        SDK.Services.ScriptClient scriptClient) : BaseDatabaseService<Game>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
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

        public async Task<SDK.Models.Manifest.Game?> GetManifestAsync(Guid id)
        {
            var game = await LoadGameForManifestAsync(id);

            return await GetManifestAsync(game);
        }

        /// <summary>
        /// Builds a manifest scoped to a specific game version, so an installed (potentially older)
        /// version resolves its own Version string and Scripts/Actions/SavePaths snapshot instead of
        /// the current latest version's config.
        /// </summary>
        public async Task<SDK.Models.Manifest.Game> GetManifestAsync(Guid id, Guid versionId)
        {
            var game = await LoadGameForManifestAsync(id);

            if (game == null)
                return null;

            var version = await gameVersionService.GetWithConfigAsync(versionId);

            return await BuildManifestAsync(game, version);
        }

        private async Task<Game> LoadGameForManifestAsync(Guid id)
        {
            return await Query(static q =>
            {
                return q
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(static g => g.Actions)
                    .Include(static g => g.Archives)
                    .Include(static g => g.BaseGame)
                    .Include(static g => g.Categories)
                    .Include(static g => g.Collections)
                    .Include(static g => g.CustomFields)
                    .Include(static g => g.DependentGames)
                    .Include(static g => g.Developers)
                    .Include(static g => g.Engine)
                    .Include(static g => g.Genres)
                    .Include(static g => g.Media)
                    .Include(static g => g.MultiplayerModes)
                    .Include(static g => g.Platforms)
                    .Include(static g => g.Publishers)
                    .Include(static g => g.Redistributables)
                        .ThenInclude(static r => r.Scripts)
                    .Include(static g => g.Tools)
                    .Include(static g => g.SavePaths)
                    .Include(static g => g.Scripts)
                    .Include(static g => g.Tags)
                    .Include(static g => g.ExternalIds);
            }).GetAsync(id);
        }

        public async Task<SDK.Models.Manifest.Game?> GetManifestAsync(Game game)
        {
            if (game == null)
                return null;

            // Resolve version-scoped config (Version, Scripts, Actions, SavePaths) from the
            // newest GameVersion so the manifest reflects that version's config snapshot rather
            // than the union of all historical (dual-written) config rows hanging off the game.
            var latestVersion = await gameVersionService.GetLatestAsync(game.Id);

            return await BuildManifestAsync(game, latestVersion);
        }

        private async Task<SDK.Models.Manifest.Game> BuildManifestAsync(Game game, GameVersion version)
        {
            if (game == null)
                return null;

            var manifest = manifestMapper.ToManifest(game);

            if (version != null)
            {
                if (!String.IsNullOrWhiteSpace(version.Version))
                    manifest.Version = version.Version;

                manifest.Scripts = version.Scripts != null
                    ? version.Scripts.Where(s => s.Type != ScriptType.Package).Select(manifestMapper.ToManifest).ToList()
                    : new List<SDK.Models.Manifest.Script>();

                manifest.Actions = version.Actions != null
                    ? version.Actions.Select(manifestMapper.ToManifest).ToList()
                    : new List<SDK.Models.Manifest.Action>();

                manifest.SavePaths = version.SavePaths != null
                    ? version.SavePaths.Select(manifestMapper.ToManifest).ToList()
                    : new List<SDK.Models.Manifest.SavePath>();
            }

            if (game.Redistributables is not { Count: > 0 })
            {
                return manifest;
            }

            using var context = await contextFactory.CreateDbContextAsync();

            foreach (var redistributable in manifest.Redistributables)
            {
                var joinEntry = await context.Set<Dictionary<string, object>>("GameRedistributable")
                    .FirstOrDefaultAsync(e =>
                        EF.Property<Guid>(e, "GameId") == game.Id &&
                        EF.Property<Guid>(e, "RedistributableId") == redistributable.Id);

                if (joinEntry != null && joinEntry.TryGetValue("Options", out var options) && options is string optionsJson && !string.IsNullOrWhiteSpace(optionsJson))
                {
                    redistributable.Options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson) ?? [];
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

            var existing = game.CustomFields.Where(c => c.Name == name).ToList();

            if (existing.Any())
                foreach (var customField in existing)
                {
                    customField.Value = value;

                    await gameCustomFieldService.UpdateAsync(customField);
                }
            else
                await gameCustomFieldService.AddAsync(new GameCustomField(name, value)
                {
                    GameId = id,
                    Game = game,
                });

            await cache.ExpireGameCacheAsync(id);

            return await GetCustomFieldAsync(id, name);
        }

        public async Task<Archive> GetLatestArchiveAsync(Guid id)
        {
            var latestVersion = await gameVersionService.GetLatestAsync(id);

            if (latestVersion?.Archive != null)
                return latestVersion.Archive;

            // Fallback for games not yet backfilled into the versioning model.
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(id);

            return game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
        }

        public async Task<string> GetVersionAsync(Guid id)
        {
            var latestVersion = await gameVersionService.GetLatestAsync(id);

            if (latestVersion != null && !String.IsNullOrWhiteSpace(latestVersion.Version))
                return latestVersion.Version;

            var latestArchive = await GetLatestArchiveAsync(id);

            return latestArchive?.Version ?? String.Empty;
        }

        public async Task<IEnumerable<Archive>> GetUpdatesAsync(Guid gameId, string version)
        {
            var newerVersions = await gameVersionService.GetNewerThanAsync(gameId, version);

            return newerVersions
                .Where(v => v.Archive != null)
                .Select(v => v.Archive)
                .ToList();
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

            var latestArchive = game.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
            var storageLocation = await storageLocationService.GetOrDefaultAsync(latestArchive?.StorageLocationId, StorageLocationType.Archive);

            string? latestArchivePath = null;
            if (latestArchive != null)
                latestArchivePath = await archiveService.GetArchiveFileLocationAsync(latestArchive);

            if (game.Scripts?.Any(s => s.Type == ScriptType.Package) ?? false)
            {
                foreach (var script in game.Scripts.Where(s => s.Type == ScriptType.Package))
                {
                    logger.LogInformation("Running script {Name} for game {GameTitle}", script.Name, game.Title);
                    var package = await scriptClient.RunPackageScriptAsync(sdkMapper.ToSdk(script), sdkMapper.ToSdk(game), latestArchivePath);

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
