using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class CollectionService(
        ILogger<CollectionService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Collection>(logger, cache, mapper, contextFactory)
    {
        public override async Task<Collection> UpdateAsync(Collection entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.Roles);
                await context.UpdateRelationshipAsync(c => c.Games);
            });
        }
    }
}
