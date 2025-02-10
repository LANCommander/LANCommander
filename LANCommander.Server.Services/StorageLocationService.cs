using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class StorageLocationService(
        ILogger<StorageLocationService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<StorageLocation>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<StorageLocation> UpdateAsync(StorageLocation entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sl => sl.Archives);
                await context.UpdateRelationshipAsync(sl => sl.GameSaves);
                await context.UpdateRelationshipAsync(sl => sl.Media);
            });
        }
    }
}
