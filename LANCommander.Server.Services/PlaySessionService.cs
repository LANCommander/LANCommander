using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PlaySessionService(
        ILogger<PlaySessionService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory,
        ServerProcessService serverProcessService,
        ServerService serverService) : BaseDatabaseService<PlaySession>(logger, cache, mapper, contextFactory)
    {
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

            var servers = await serverService.GetAsync(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

            foreach (var server in servers)
            {
                serverProcessService.StartServerAsync(server.Id);
            }
        }

        public async Task EndSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await UpdateAsync(existingSession);
            }

            var activeSessions = (await GetAsync(ps => ps.GameId == gameId && ps.End == null)).Any();

            if (!activeSessions)
            {
                var servers = await serverService.GetAsync(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

                foreach (var server in servers)
                {
                    serverProcessService.StopServerAsync(server.Id);
                }
            }
        }
    }
}
