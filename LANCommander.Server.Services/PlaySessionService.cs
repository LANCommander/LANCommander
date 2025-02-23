using AutoMapper;
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
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ServerProcessService serverProcessService,
        ServerService serverService) : BaseDatabaseService<PlaySession>(logger, cache, mapper, httpContextAccessor, contextFactory)
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
        }

        public async Task EndSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await UpdateAsync(existingSession);
            }
        }
    }
}
