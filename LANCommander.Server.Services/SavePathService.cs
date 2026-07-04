using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
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
            var added = await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sp => sp.Game);
            });

            await TouchGameAsync(added.GameId);

            return added;
        }

        public async override Task<SavePath> UpdateAsync(SavePath entity)
        {
            var updated = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sp => sp.Game);
            });

            await TouchGameAsync(updated.GameId);

            return updated;
        }

        public override async Task DeleteAsync(SavePath entity)
        {
            var gameId = entity.GameId;

            await base.DeleteAsync(entity);

            await TouchGameAsync(gameId);
        }

        private async Task TouchGameAsync(Guid? gameId)
        {
            if (!gameId.HasValue)
                return;

            await using var context = await contextFactory.CreateDbContextAsync();

            var game = await context.Games.FirstOrDefaultAsync(g => g.Id == gameId.Value);

            if (game != null)
            {
                game.UpdatedOn = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }

            await cache.ExpireGameCacheAsync(gameId.Value);
        }
    }
}
