using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class GameVersionService(
        ILogger<GameVersionService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<GameVersion>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<GameVersion> AddAsync(GameVersion entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(v => v.Game);
            });
        }

        public override async Task<GameVersion> UpdateAsync(GameVersion entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(v => v.Archive);
                await context.UpdateRelationshipAsync(v => v.Scripts);
                await context.UpdateRelationshipAsync(v => v.Actions);
                await context.UpdateRelationshipAsync(v => v.SavePaths);
            });
        }

        /// <summary>
        /// Returns the newest version for a game using the canonical resolver
        /// (highest SortOrder, with CreatedOn as a tiebreaker).
        /// </summary>
        public async Task<GameVersion?> GetLatestAsync(Guid gameId)
        {
            return await Query(q => q
                    .Where(v => v.GameId == gameId)
                    .OrderByDescending(v => v.SortOrder)
                    .ThenByDescending(v => v.CreatedOn))
                .Include(v => v.Archive)
                .Include(v => v.Scripts)
                .Include(v => v.Actions)
                .Include(v => v.SavePaths)
                .FirstOrDefaultAsync(v => true);
        }

        /// <summary>
        /// The version launchers are offered: the newest published version, or the newest version when none
        /// is published yet (a game that hasn't been through publishing works as before). Loaded with its
        /// config.
        /// </summary>
        public async Task<GameVersion?> GetCurrentAsync(Guid gameId)
        {
            var currentId = await GetCurrentIdAsync(gameId);

            return currentId == null ? null : await GetWithConfigAsync(currentId.Value);
        }

        /// <summary>The id of the version launchers are offered; see <see cref="GetCurrentAsync"/>.</summary>
        public async Task<Guid?> GetCurrentIdAsync(Guid gameId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var versions = await context.GameVersions
                .Where(v => v.GameId == gameId)
                .OrderByDescending(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedOn)
                .Select(v => new { v.Id, v.Published })
                .ToListAsync();

            return (versions.FirstOrDefault(v => v.Published) ?? versions.FirstOrDefault())?.Id;
        }

        /// <summary>
        /// Publishes or unpublishes a version. Launchers are offered the newest published version, so this
        /// can change what they install and update to.
        /// </summary>
        public async Task SetPublishedAsync(Guid versionId, bool published)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                var version = await context.GameVersions.FirstOrDefaultAsync(v => v.Id == versionId);

                if (version == null || version.Published == published)
                    return;

                version.Published = published;

                await context.SaveChangesAsync();
            }

            // The game's own config mirrors the version launchers are offered, which may have changed
            await MirrorCurrentToGameAsync(versionId, force: true);
        }

        /// <summary>
        /// Returns the newest version for a game, creating an empty initial version if the game
        /// has none yet. Used by the config editors so version-scoped config (Scripts, Actions,
        /// SavePaths) always has a version to attach to, even before the first archive is uploaded.
        /// </summary>
        public async Task<GameVersion> GetOrCreateLatestAsync(Guid gameId)
        {
            var latest = await GetLatestAsync(gameId);

            if (latest != null)
                return latest;

            return await CreateAsync(gameId, string.Empty);
        }

        /// <summary>
        /// Returns the id of the newest version for a game, or null if it has none. Lightweight
        /// companion to <see cref="GetLatestAsync"/> for callers that only need to associate an
        /// entity with the current version and don't need the full version graph loaded.
        /// </summary>
        public async Task<Guid?> GetLatestIdAsync(Guid gameId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            return await context.GameVersions
                .Where(v => v.GameId == gameId)
                .OrderByDescending(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedOn)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Returns the version number of the newest version for a game, or null if it has none.
        /// Lightweight projection for callers that only need the version string (e.g. to display
        /// it or to prepopulate an upload form).
        /// </summary>
        public async Task<string?> GetLatestVersionNumberAsync(Guid gameId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            return await context.GameVersions
                .Where(v => v.GameId == gameId)
                .OrderByDescending(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedOn)
                .Select(v => v.Version)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Returns the id of the newest version for a game, creating an empty initial version if
        /// none exists yet. Lightweight companion to <see cref="GetOrCreateLatestAsync"/>.
        /// </summary>
        public async Task<Guid> GetOrCreateLatestIdAsync(Guid gameId)
        {
            var latestId = await GetLatestIdAsync(gameId);

            if (latestId.HasValue)
                return latestId.Value;

            return (await CreateAsync(gameId, string.Empty)).Id;
        }

        /// <summary>
        /// Returns the versions newer than the supplied version string, ordered oldest-to-newest.
        /// If the version is unknown or blank, only the latest version is returned.
        /// </summary>
        public Task<IEnumerable<GameVersion>> GetNewerThanAsync(Guid gameId, string version)
            => GetNewerThanAsync(gameId, null, version);

        /// <summary>
        /// Returns the versions newer than the installed one (resolved by id, then by version string) up to
        /// the version launchers are offered, ordered oldest-to-newest. If the installed version can't be
        /// resolved, only the offered version is returned.
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetNewerThanAsync(Guid gameId, Guid? versionId, string? version)
        {
            var newestFirst = (await GetAllAsync(gameId)).ToList();

            if (newestFirst.Count == 0)
                return [];

            // Updates go as far as the version launchers are offered; newer drafts aren't offered. Drafts
            // between the installed and offered versions are included, since their archives make up the
            // offered version's files.
            var current = newestFirst.FirstOrDefault(v => v.Published) ?? newestFirst.First();
            var offered = newestFirst.SkipWhile(v => v.Id != current.Id).ToList();

            var installed = ResolveInstalled(newestFirst, versionId, version);

            if (installed == null)
                return [current];

            return offered
                .TakeWhile(v => v.Id != installed.Id && v.SortOrder > installed.SortOrder)
                .Reverse()
                .ToList();
        }

        /// <summary>
        /// Returns every version for a game, newest first (highest SortOrder, CreatedOn tiebreaker),
        /// with each version's Archive loaded. Used to present the full version list to clients.
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetAllAsync(Guid gameId)
        {
            return (await Query(q => q.Where(v => v.GameId == gameId))
                    .Include(v => v.Archive)
                    .GetAsync(v => v.GameId == gameId))
                .OrderByDescending(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedOn)
                .ToList();
        }

        /// <summary>
        /// Returns a single version with its full config graph (Archive, Scripts, Actions,
        /// SavePaths) loaded. Used to build a version-scoped manifest for a specific version.
        /// </summary>
        public async Task<GameVersion?> GetWithConfigAsync(Guid versionId)
        {
            return await Include(v => v.Archive)
                .Include(v => v.Scripts)
                .Include(v => v.Actions)
                .Include(v => v.SavePaths)
                .GetAsync(versionId);
        }

        /// <summary>
        /// Creates a new version for a game, as a draft; a current version that is still a draft is
        /// published. A game's first version is published when the game is. The new version copies the version-scoped
        /// configuration (Scripts, Actions, SavePaths, option schema and redistributables with their
        /// options) from the current latest version into fresh rows so each version owns an
        /// independent snapshot of its config. A game's first version starts from the game's own
        /// option schema and redistributables.
        /// </summary>
        public async Task<GameVersion> CreateAsync(Guid gameId, string version, string? changelog = null)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var nextSortOrder = await context.GameVersions
                .Where(v => v.GameId == gameId)
                .Select(v => (int?)v.SortOrder)
                .MaxAsync() ?? -1;

            var previous = await context.GameVersions
                .AsNoTracking()
                .Include(v => v.Scripts)
                .Include(v => v.Actions)
                .Include(v => v.SavePaths)
                .Where(v => v.GameId == gameId)
                .OrderByDescending(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedOn)
                .FirstOrDefaultAsync();

            // Moving on from a draft releases it: creating a newer version publishes the current one
            if (previous is { Published: false })
            {
                var draft = await context.GameVersions.FirstAsync(v => v.Id == previous.Id);

                draft.Published = true;
            }

            var gameVersion = new GameVersion
            {
                GameId = gameId,
                Version = version,
                Changelog = changelog,
                SortOrder = nextSortOrder + 1,
                Published = false,
                CreatedOn = DateTime.UtcNow,
                Scripts = previous?.Scripts?.Select(s => CopyScript(s, gameId)).ToList() ?? new List<Script>(),
                Actions = previous?.Actions?.Select(a => CopyAction(a, gameId)).ToList() ?? new List<Data.Models.Action>(),
                SavePaths = previous?.SavePaths?.Select(p => CopySavePath(p, gameId)).ToList() ?? new List<SavePath>(),
            };

            if (previous != null)
            {
                gameVersion.OptionSchema = previous.OptionSchema;
                gameVersion.Redistributables = (await context.GameVersionRedistributables
                        .AsNoTracking()
                        .Where(r => r.GameVersionId == previous.Id)
                        .ToListAsync())
                    .Select(r => new GameVersionRedistributable { RedistributableId = r.RedistributableId, Options = r.Options })
                    .ToList();
            }
            else
            {
                // A game's first version is offered to launchers when the game is, as the backfill does
                gameVersion.Published = await context.Games
                    .Where(g => g.Id == gameId)
                    .Select(g => g.Published)
                    .FirstOrDefaultAsync();

                gameVersion.OptionSchema = await context.Games
                    .Where(g => g.Id == gameId)
                    .Select(g => g.OptionSchema)
                    .FirstOrDefaultAsync();
                gameVersion.Redistributables = (await GetGameRedistributableRowsAsync(context, gameId))
                    .Select(r => new GameVersionRedistributable { RedistributableId = r.RedistributableId, Options = r.Options })
                    .ToList();
            }

            context.GameVersions.Add(gameVersion);

            await context.SaveChangesAsync();
            await cache.ExpireGameCacheAsync(gameId);

            return gameVersion;
        }

        /// <summary>Whether the version is its game's newest (highest SortOrder, CreatedOn tiebreaker).</summary>
        public async Task<bool> IsLatestAsync(Guid versionId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var gameId = await context.GameVersions
                .Where(v => v.Id == versionId)
                .Select(v => (Guid?)v.GameId)
                .FirstOrDefaultAsync();

            return gameId != null && await GetLatestIdAsync(gameId.Value) == versionId;
        }

        /// <summary>The redistributables a version needs, with their option values and the redistributable loaded.</summary>
        public async Task<List<GameVersionRedistributable>> GetRedistributablesAsync(Guid versionId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            return await context.GameVersionRedistributables
                .AsNoTracking()
                .Include(r => r.Redistributable)
                .Where(r => r.GameVersionId == versionId)
                .ToListAsync();
        }

        /// <summary>
        /// Sets which redistributables a version needs. Redistributables it keeps keep their option values.
        /// Changing the latest version also updates the game's own list, which mirrors the latest version.
        /// </summary>
        public async Task SetRedistributablesAsync(Guid versionId, IEnumerable<Guid> redistributableIds)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var wanted = redistributableIds.ToHashSet();

            var existing = await context.GameVersionRedistributables
                .Where(r => r.GameVersionId == versionId)
                .ToListAsync();

            context.GameVersionRedistributables.RemoveRange(existing.Where(r => !wanted.Contains(r.RedistributableId)));

            foreach (var id in wanted.Where(id => existing.All(r => r.RedistributableId != id)))
                context.GameVersionRedistributables.Add(new GameVersionRedistributable { GameVersionId = versionId, RedistributableId = id });

            await context.SaveChangesAsync();

            await MirrorLatestToGameAsync(versionId);
        }

        public async Task<string?> GetRedistributableOptionsAsync(Guid versionId, Guid redistributableId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            return await context.GameVersionRedistributables
                .Where(r => r.GameVersionId == versionId && r.RedistributableId == redistributableId)
                .Select(r => r.Options)
                .FirstOrDefaultAsync();
        }

        /// <summary>Sets a version's values for a redistributable's options; it must already be one of the version's redistributables.</summary>
        public async Task SetRedistributableOptionsAsync(Guid versionId, Guid redistributableId, string? optionsJson)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var row = await context.GameVersionRedistributables
                .FirstOrDefaultAsync(r => r.GameVersionId == versionId && r.RedistributableId == redistributableId);

            if (row == null)
                return;

            row.Options = optionsJson;

            await context.SaveChangesAsync();

            await MirrorLatestToGameAsync(versionId);
        }

        public async Task<string?> GetOptionSchemaAsync(Guid versionId)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            return await context.GameVersions
                .Where(v => v.Id == versionId)
                .Select(v => v.OptionSchema)
                .FirstOrDefaultAsync();
        }

        public async Task SetOptionSchemaAsync(Guid versionId, string? optionSchema)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var version = await context.GameVersions.FirstOrDefaultAsync(v => v.Id == versionId);

            if (version == null)
                return;

            version.OptionSchema = optionSchema;

            await context.SaveChangesAsync();

            await MirrorLatestToGameAsync(versionId);
        }

        /// <summary>
        /// The game's own option schema and redistributables mirror the version launchers are offered, so
        /// readers that aren't version-aware (the game list, depot, script cache invalidation) see current
        /// config. Does nothing for other versions.
        /// </summary>
        private async Task MirrorLatestToGameAsync(Guid versionId) => await MirrorCurrentToGameAsync(versionId, force: false);

        /// <param name="force">Mirror the game's current version even when <paramref name="versionId"/> isn't it.</param>
        private async Task MirrorCurrentToGameAsync(Guid versionId, bool force)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            var version = await context.GameVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId);

            if (version == null)
            {
                return;
            }

            var currentId = await GetCurrentIdAsync(version.GameId);

            if (currentId != versionId)
            {
                if (!force || currentId == null)
                {
                    await cache.ExpireGameCacheAsync(version.GameId);
                    return;
                }

                versionId = currentId.Value;
                version = await context.GameVersions.AsNoTracking().FirstAsync(v => v.Id == versionId);
            }

            var rows = await context.GameVersionRedistributables
                .AsNoTracking()
                .Where(r => r.GameVersionId == versionId)
                .ToListAsync();

            var game = await context.Games
                .Include(g => g.Redistributables)
                .FirstAsync(g => g.Id == version.GameId);

            game.OptionSchema = version.OptionSchema;

            var wanted = rows.Select(r => r.RedistributableId).ToHashSet();

            foreach (var redistributable in game.Redistributables!.Where(r => !wanted.Contains(r.Id)).ToList())
                game.Redistributables.Remove(redistributable);

            foreach (var id in wanted.Where(id => game.Redistributables.All(r => r.Id != id)))
                game.Redistributables.Add(await context.Set<Redistributable>().FirstAsync(r => r.Id == id));

            await context.SaveChangesAsync();

            // The join rows exist now; copy the option values onto them
            var joinRows = await context.Set<Dictionary<string, object>>("GameRedistributable")
                .Where(e => EF.Property<Guid>(e, "GameId") == version.GameId)
                .ToListAsync();

            foreach (var joinRow in joinRows)
            {
                var redistributableId = (Guid)joinRow["RedistributableId"];

                joinRow["Options"] = rows.FirstOrDefault(r => r.RedistributableId == redistributableId)?.Options!;
            }

            await context.SaveChangesAsync();
            await cache.ExpireGameCacheAsync(version.GameId);
        }

        private static async Task<List<GameVersionRedistributable>> GetGameRedistributableRowsAsync(DatabaseContext context, Guid gameId)
        {
            var joinRows = await context.Set<Dictionary<string, object>>("GameRedistributable")
                .AsNoTracking()
                .Where(e => EF.Property<Guid>(e, "GameId") == gameId)
                .ToListAsync();

            return joinRows
                .Select(e => new GameVersionRedistributable
                {
                    RedistributableId = (Guid)e["RedistributableId"],
                    Options = e.TryGetValue("Options", out var options) ? options as string : null,
                })
                .ToList();
        }

        /// <summary>
        /// Links a game archive to the version it belongs to. An explicit <paramref name="targetVersionId"/>
        /// wins; otherwise the newest version with the archive's version string is used, then an empty
        /// placeholder latest version (filled in with the archive's version), and failing those a new
        /// version is created. A version that already had a different archive has that archive unlinked;
        /// the old archive is kept so an admin can remove it.
        /// </summary>
        public async Task<GameVersion?> LinkArchiveAsync(Guid archiveId, Guid? targetVersionId = null, string? changelog = null)
        {
            Guid gameId;
            string archiveVersion;

            using (var context = await contextFactory.CreateDbContextAsync())
            {
                var archive = await context.Set<Archive>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == archiveId);

                if (archive?.GameId is not { } archiveGameId || archiveGameId == Guid.Empty)
                    return null;

                gameId = archiveGameId;
                archiveVersion = archive.Version ?? string.Empty;
            }

            var versionId = await ResolveVersionForArchiveAsync(gameId, archiveVersion, targetVersionId, changelog);

            using (var context = await contextFactory.CreateDbContextAsync())
            {
                var version = await context.GameVersions
                    .Include(v => v.Archive)
                    .FirstAsync(v => v.Id == versionId);

                if (version.Archive != null && version.Archive.Id != archiveId)
                    version.Archive.GameVersionId = null;

                var archive = await context.Set<Archive>().FirstAsync(a => a.Id == archiveId);

                // One archive per version, so this also detaches it from any version it was on before
                archive.GameVersionId = version.Id;

                if (String.IsNullOrWhiteSpace(version.Version) && !String.IsNullOrWhiteSpace(archiveVersion))
                    version.Version = archiveVersion;

                if (String.IsNullOrWhiteSpace(version.Changelog) && !String.IsNullOrWhiteSpace(changelog))
                    version.Changelog = changelog;

                await context.SaveChangesAsync();
                await cache.ExpireGameCacheAsync(gameId);

                return version;
            }
        }

        private async Task<Guid> ResolveVersionForArchiveAsync(Guid gameId, string archiveVersion, Guid? targetVersionId, string? changelog)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (targetVersionId is { } targetId && targetId != Guid.Empty
                    && await context.GameVersions.AnyAsync(v => v.Id == targetId && v.GameId == gameId))
                    return targetId;

                var versions = await context.GameVersions
                    .AsNoTracking()
                    .Where(v => v.GameId == gameId)
                    .OrderByDescending(v => v.SortOrder)
                    .ThenByDescending(v => v.CreatedOn)
                    .Select(v => new { v.Id, v.Version, HasArchive = v.Archive != null })
                    .ToListAsync();

                if (!String.IsNullOrWhiteSpace(archiveVersion))
                {
                    var match = versions.FirstOrDefault(v => v.Version == archiveVersion);

                    if (match != null)
                        return match.Id;
                }

                var latest = versions.FirstOrDefault();

                if (latest != null && String.IsNullOrWhiteSpace(latest.Version) && !latest.HasArchive)
                    return latest.Id;
            }

            return (await CreateAsync(gameId, archiveVersion, changelog)).Id;
        }

        /// <summary>
        /// Finds the version a client has installed, by id when it knows one and otherwise by version
        /// string (newest match wins). Returns null when neither identifies a version of the game.
        /// </summary>
        public async Task<GameVersion?> ResolveInstalledAsync(Guid gameId, Guid? versionId, string? version)
        {
            var versions = (await GetAllAsync(gameId)).ToList();

            return ResolveInstalled(versions, versionId, version);
        }

        private static GameVersion? ResolveInstalled(IEnumerable<GameVersion> newestFirst, Guid? versionId, string? version)
        {
            if (versionId is { } id && id != Guid.Empty)
            {
                var byId = newestFirst.FirstOrDefault(v => v.Id == id);

                if (byId != null)
                    return byId;
            }

            if (String.IsNullOrWhiteSpace(version))
                return null;

            return newestFirst.FirstOrDefault(v => v.Version == version);
        }

        private static Script CopyScript(Script source, Guid gameId) => new()
        {
            GameId = gameId,
            Name = source.Name,
            Description = source.Description,
            Type = source.Type,
            Contents = source.Contents,
            RequiresAdmin = source.RequiresAdmin,
            CreatedOn = DateTime.UtcNow,
        };

        private static Data.Models.Action CopyAction(Data.Models.Action source, Guid gameId) => new()
        {
            GameId = gameId,
            Name = source.Name,
            Arguments = source.Arguments,
            Path = source.Path,
            WorkingDirectory = source.WorkingDirectory,
            PrimaryAction = source.PrimaryAction,
            SortOrder = source.SortOrder,
            OptionOverrides = source.OptionOverrides,
            CreatedOn = DateTime.UtcNow,
        };

        private static SavePath CopySavePath(SavePath source, Guid gameId) => new()
        {
            GameId = gameId,
            Type = source.Type,
            Path = source.Path,
            WorkingDirectory = source.WorkingDirectory,
            IsRegex = source.IsRegex,
            CreatedOn = DateTime.UtcNow,
        };
    }
}
