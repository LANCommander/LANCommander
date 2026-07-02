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
        GameVersionService gameVersionService,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Action>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Action> AddAsync(Action entity)
        {
            // Actions for a game are version-scoped. Attach new actions to the game's current
            // version here so callers don't have to resolve and pass it in themselves.
            if (entity.GameId.HasValue && entity.GameId != Guid.Empty
                && (entity.GameVersionId == null || entity.GameVersionId == Guid.Empty))
                entity.GameVersionId = await gameVersionService.GetOrCreateLatestIdAsync(entity.GameId.Value);

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Server);
                await context.UpdateRelationshipAsync(a => a.Tool);
            });
        }

        public async override Task<Action> UpdateAsync(Action entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Server);
                await context.UpdateRelationshipAsync(a => a.Tool);
            });
        }
    }
}
