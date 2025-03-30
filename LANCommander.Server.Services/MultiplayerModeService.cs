using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class MultiplayerModeService(
        ILogger<MultiplayerModeService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<MultiplayerMode>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<MultiplayerMode> AddAsync(MultiplayerMode entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
            });
        }

        public async override Task<MultiplayerMode> UpdateAsync(MultiplayerMode entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
            });
        }
    }
}
