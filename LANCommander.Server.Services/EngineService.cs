using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services
{
    public sealed class EngineService(
        ILogger<EngineService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Engine>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Engine> AddAsync(Engine entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(e => e.Games);
            });
        }

        public override async Task<Engine> UpdateAsync(Engine entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(e => e.Games);
            });
        }

        public override async Task DeleteAsync(Engine entity)
        {
            await cache.ExpireGameCacheAsync();

            await base.DeleteAsync(entity);
        }
    }
}
