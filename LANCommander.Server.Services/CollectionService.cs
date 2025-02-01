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
        public override Task<Collection> UpdateAsync(Collection entity)
        {
            throw new NotImplementedException();
        }
    }
}
