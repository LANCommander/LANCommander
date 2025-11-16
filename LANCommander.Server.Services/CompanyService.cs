using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class CompanyService(
        ILogger<CompanyService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Company>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Company> AddAsync(Company entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.DevelopedGames);
                await context.UpdateRelationshipAsync(c => c.PublishedGames);
            });
        }

        public override async Task<Company> UpdateAsync(Company entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.DevelopedGames);
                await context.UpdateRelationshipAsync(c => c.PublishedGames);
            });
        }
    }
}
