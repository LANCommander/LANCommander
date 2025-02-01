using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PlatformService(
        ILogger<PlatformService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Platform>(logger, cache, mapper, contextFactory)
    {
        public override Task<Platform> UpdateAsync(Platform entity)
        {
            throw new NotImplementedException();
        }
    }
}
