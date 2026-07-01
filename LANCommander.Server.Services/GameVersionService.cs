using AutoMapper;
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
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<GameVersion>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
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
        public async Task<IEnumerable<GameVersion>> GetNewerThanAsync(Guid gameId, string version)
        {
            var versions = (await Query(q => q.Where(v => v.GameId == gameId))
                    .Include(v => v.Archive)
                    .GetAsync(v => v.GameId == gameId))
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.CreatedOn)
                .ToList();

            if (versions.Count == 0)
                return [];

            if (string.IsNullOrWhiteSpace(version))
                return [versions.Last()];

            var installed = versions.LastOrDefault(v => v.Version == version);

            if (installed == null)
                return [versions.Last()];

            return versions
                .SkipWhile(v => v.Id != installed.Id)
                .Skip(1)
                .ToList();
        }

        /// <summary>
        /// Creates a new version for a game. The new version copies the version-scoped
        /// configuration (Scripts, Actions, SavePaths) from the current latest version
        /// into fresh rows so each version owns an independent snapshot of its config.
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

            var gameVersion = new GameVersion
            {
                GameId = gameId,
                Version = version,
                Changelog = changelog,
                SortOrder = nextSortOrder + 1,
                CreatedOn = DateTime.UtcNow,
                Scripts = previous?.Scripts?.Select(s => CopyScript(s, gameId)).ToList() ?? new List<Script>(),
                Actions = previous?.Actions?.Select(a => CopyAction(a, gameId)).ToList() ?? new List<Data.Models.Action>(),
                SavePaths = previous?.SavePaths?.Select(p => CopySavePath(p, gameId)).ToList() ?? new List<SavePath>(),
            };

            context.GameVersions.Add(gameVersion);

            await context.SaveChangesAsync();
            await cache.ExpireGameCacheAsync(gameId);

            return gameVersion;
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
