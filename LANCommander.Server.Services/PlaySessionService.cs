using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PlaySessionService(
        ILogger<PlaySessionService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        PlaySessionKeepAliveTracker keepAliveTracker,
        ServerService serverService) : BaseDatabaseService<PlaySession>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<PlaySession> AddAsync(PlaySession entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(ps => ps.Game);
                await context.UpdateRelationshipAsync(ps => ps.User);
            });
        }

        public override async Task<PlaySession> UpdateAsync(PlaySession entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(ps => ps.Game);
                await context.UpdateRelationshipAsync(ps => ps.User);
            });
        }

        public async Task StartSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
                await DeleteAsync(existingSession);

            var session = new PlaySession()
            {
                GameId = gameId,
                UserId = userId,
                Start = DateTime.UtcNow
            };

            await AddAsync(session);

            await keepAliveTracker.TouchAsync(gameId, userId);
        }

        public async Task EndSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await UpdateAsync(existingSession);
            }

            await keepAliveTracker.RemoveAsync(gameId, userId);
        }

        /// <summary>
        /// Records that the player is still in the game. Tracked via the cache; the sweep uses it to
        /// detect sessions that have gone silent.
        /// </summary>
        public async Task KeepAliveAsync(Guid gameId, Guid userId)
        {
            await keepAliveTracker.TouchAsync(gameId, userId);
        }

        /// <summary>
        /// Ends any active session whose last keepalive is older than <paramref name="timeout"/>,
        /// setting its End to that last known-alive time. Returns the affected games so callers can
        /// schedule server autostop.
        /// </summary>
        public async Task<IEnumerable<Guid>> EndStaleSessionsAsync(TimeSpan timeout)
        {
            var cutoff = DateTime.UtcNow - timeout;

            var activeSessions = await GetAsync(ps => ps.End == null);

            var affectedGameIds = new HashSet<Guid>();

            foreach (var session in activeSessions)
            {
                var lastSeen = await keepAliveTracker.GetOrSeedAsync(session.GameId.GetValueOrDefault(), session.UserId);

                if (lastSeen >= cutoff)
                    continue;

                session.End = lastSeen;

                await UpdateAsync(session);

                await keepAliveTracker.RemoveAsync(session.GameId.GetValueOrDefault(), session.UserId);

                if (session.GameId.HasValue)
                    affectedGameIds.Add(session.GameId.Value);
            }

            return affectedGameIds;
        }
    }
}
