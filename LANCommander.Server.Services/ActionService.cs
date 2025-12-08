using AutoMapper;
using LANCommander.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using Action = LANCommander.Server.Data.Models.Action;

namespace LANCommander.Server.Services
{
    public sealed class ActionService(
        ILogger<ActionService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Action>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Action> AddAsync(Action entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Server);
            });
        }

        public async override Task<Action> UpdateAsync(Action entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Server);
            });
        }
    }
}
