using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class SavePathService(
        ILogger<SavePathService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<SavePath>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<SavePath> AddAsync(SavePath entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sp => sp.Game);
            });
        }

        public async override Task<SavePath> UpdateAsync(SavePath entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sp => sp.Game);
            });
        }
    }
}
