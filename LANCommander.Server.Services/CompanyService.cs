using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services
{
    public sealed class CompanyService(
        ILogger<CompanyService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Company>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Company> AddAsync(Company entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.DevelopedGames);
                await context.UpdateRelationshipAsync(c => c.PublishedGames);
            });
        }

        public override async Task<Company> UpdateAsync(Company entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.DevelopedGames);
                await context.UpdateRelationshipAsync(c => c.PublishedGames);
            });
        }

        public override async Task DeleteAsync(Company entity)
        {
            await cache.ExpireGameCacheAsync();

            await base.DeleteAsync(entity);
        }
    }
}
