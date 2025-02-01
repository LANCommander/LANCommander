using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class SavePathService(
        ILogger<SavePathService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<SavePath>(logger, cache, mapper, contextFactory)
    {
        public override Task<SavePath> UpdateAsync(SavePath entity)
        {
            throw new NotImplementedException();
        }
    }
}
