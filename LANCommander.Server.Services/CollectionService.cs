using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services
{
    public sealed class CollectionService(
        ILogger<CollectionService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Collection>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Collection> AddAsync(Collection entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.Roles);
                await context.UpdateRelationshipAsync(c => c.Games);
            });
        }

        public override async Task<Collection> UpdateAsync(Collection entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.Roles);
                await context.UpdateRelationshipAsync(c => c.Games);
            });
        }

        public override async Task DeleteAsync(Collection entity)
        {
            await cache.ExpireGameCacheAsync();

            await base.DeleteAsync(entity);
        }
    }
}
