using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services
{
    public sealed class PlatformService(
        ILogger<PlatformService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Platform>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Platform> AddAsync(Platform entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.Games);
            });
        }

        public override async Task<Platform> UpdateAsync(Platform entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.Games);
            });
        }

        public override async Task DeleteAsync(Platform entity)
        {
            await cache.ExpireGameCacheAsync();

            await base.DeleteAsync(entity);
        }
    }
}
