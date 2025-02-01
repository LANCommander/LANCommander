using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class GenreService(
        ILogger<GenreService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Genre>(logger, cache, mapper, contextFactory)
    {
        public override Task<Genre> UpdateAsync(Genre entity)
        {
            throw new NotImplementedException();
        }
    }
}
