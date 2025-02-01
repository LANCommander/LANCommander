using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class StorageLocationService(
        ILogger<StorageLocationService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<StorageLocation>(logger, cache, mapper, contextFactory)
    {
        public override Task<StorageLocation> UpdateAsync(StorageLocation entity)
        {
            throw new NotImplementedException();
        }
    }
}
