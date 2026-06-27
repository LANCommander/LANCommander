using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    /// <summary>
    /// Idempotent startup routine that seeds the GameVersion table for games that predate
    /// first-class versioning. Each existing archive becomes its own version (ordered oldest
    /// to newest), and the game's current version-scoped config (Scripts, Actions, SavePaths)
    /// is attached to the newest version. Games with no archives receive a single empty
    /// version so their config still has a home.
    /// </summary>
    public static class GameVersionBackfill
    {
        public static async Task RunAsync(IDbContextFactory<DatabaseContext> contextFactory, ILogger logger)
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            // Only consider games that don't already have versions, so this is safe to run on every startup.
            var gameIds = await context.Games
                .Where(g => !g.Versions.Any())
                .Select(g => g.Id)
                .ToListAsync();

            if (gameIds.Count == 0)
            {
                logger.LogDebug("GameVersion backfill: no games require backfilling");
                return;
            }

            logger.LogInformation("GameVersion backfill: seeding versions for {GameCount} game(s)", gameIds.Count);

            foreach (var gameId in gameIds)
            {
                var game = await context.Games.FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                    continue;

                // Load version-scoped config from the dependent side (filtered by GameId) rather than
                // through Game's collection navigations. This is equivalent in relational providers and
                // avoids the EF InMemory provider's collection-navigation loading gap relied on by tests.
                var orderedArchives = await context.Set<Archive>()
                    .Where(a => a.GameId == gameId)
                    .OrderBy(a => a.CreatedOn)
                    .ToListAsync();

                var scripts = await context.Set<Script>().Where(s => s.GameId == gameId).ToListAsync();
                var actions = await context.Set<LANCommander.Server.Data.Models.Action>().Where(a => a.GameId == gameId).ToListAsync();
                var savePaths = await context.Set<SavePath>().Where(p => p.GameId == gameId).ToListAsync();

                var versions = new List<GameVersion>();

                if (orderedArchives.Count > 0)
                {
                    for (var i = 0; i < orderedArchives.Count; i++)
                    {
                        var archive = orderedArchives[i];

                        var version = new GameVersion
                        {
                            Id = Guid.NewGuid(),
                            GameId = game.Id,
                            Version = string.IsNullOrWhiteSpace(archive.Version) ? $"{i + 1}" : archive.Version,
                            Changelog = archive.Changelog,
                            SortOrder = i,
                            CreatedOn = archive.CreatedOn,
                        };

                        archive.GameVersion = version;
                        context.GameVersions.Add(version);
                        versions.Add(version);
                    }
                }
                else
                {
                    var version = new GameVersion
                    {
                        Id = Guid.NewGuid(),
                        GameId = game.Id,
                        Version = string.Empty,
                        SortOrder = 0,
                        CreatedOn = game.CreatedOn,
                    };

                    context.GameVersions.Add(version);
                    versions.Add(version);
                }

                // Attach the game's existing config to the newest version. Existing GameId values
                // are preserved (dual-write) so any remaining game-scoped queries keep working.
                var newest = versions.Last();

                foreach (var script in scripts)
                    script.GameVersion = newest;

                foreach (var action in actions)
                    action.GameVersion = newest;

                foreach (var savePath in savePaths)
                    savePath.GameVersion = newest;

                await context.SaveChangesAsync();
            }

            logger.LogInformation("GameVersion backfill complete");
        }
    }
}
