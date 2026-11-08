using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins;
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
        ServerService serverService,
        IPluginEventBus pluginEventBus) : BaseDatabaseService<PlaySession>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
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

        /// <param name="serverId">The game server the player is joining, when the launcher knows it.</param>
        /// <param name="alias">The name the player plays under, used to link them to match participants.</param>
        public async Task StartSessionAsync(Guid gameId, Guid userId, Guid? serverId = null, string? alias = null)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
                await DeleteAsync(existingSession);

            var session = new PlaySession()
            {
                GameId = gameId,
                UserId = userId,
                ServerId = await ExistingServerIdAsync(serverId),
                Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim()[..Math.Min(255, alias.Trim().Length)],
                Start = DateTime.UtcNow
            };

            session = await AddAsync(session);

            await keepAliveTracker.TouchAsync(gameId, userId);

            await pluginEventBus.PublishAsync(new PlaySessionStartedEvent(session.Id, gameId, userId, session.Start ?? session.CreatedOn));
        }

        /// <summary>
        /// Records which game server the player's open session for the game is on, once the launcher
        /// sees the game connect to it. Returns false when there's no open session or no such server.
        /// </summary>
        public async Task<bool> SetSessionServerAsync(Guid gameId, Guid userId, Guid serverId)
        {
            var session = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (session == null || await ExistingServerIdAsync(serverId) == null)
                return false;

            if (session.ServerId == serverId)
                return true;

            session.ServerId = serverId;

            await UpdateAsync(session);

            return true;
        }

        private async Task<Guid?> ExistingServerIdAsync(Guid? serverId)
        {
            if (serverId is not { } id || id == Guid.Empty)
                return null;

            await using var context = await contextFactory.CreateDbContextAsync();

            return await context.Servers!.AnyAsync(s => s.Id == id) ? id : null;
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

            if (existingSession != null)
                await PublishEndedAsync(existingSession, timedOut: false);
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

                await PublishEndedAsync(session, timedOut: true);
            }

            return affectedGameIds;
        }

        private async Task PublishEndedAsync(PlaySession session, bool timedOut)
        {
            // A session whose game has since been deleted has nothing to report against
            if (session.GameId is not { } gameId || session.End is not { } end)
                return;

            await pluginEventBus.PublishAsync(new PlaySessionEndedEvent(session.Id, gameId, session.UserId, session.Start ?? session.CreatedOn, end, timedOut));
        }
    }
}
