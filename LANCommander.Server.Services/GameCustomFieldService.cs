using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using System.Linq.Expressions;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services
{
    public class GameCustomFieldService(
        ILogger<GameCustomFieldService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<GameCustomField>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<GameCustomField> AddAsync(GameCustomField entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(cf => cf.Game);
            });
        }

        public override async Task<ExistingEntityResult<GameCustomField>> AddMissingAsync(Expression<Func<GameCustomField, bool>> predicate, GameCustomField entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<GameCustomField> UpdateAsync(GameCustomField entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);
            
            var update = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(g => g.Game);
            });

            return update;
        }

        public override async Task DeleteAsync(GameCustomField gameCustomField)
        {
            gameCustomField = await GetAsync(gameCustomField.Id);

            await cache.ExpireGameCacheAsync(gameCustomField.GameId);
            await base.DeleteAsync(gameCustomField);
        }
    }
}
