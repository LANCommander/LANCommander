using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class CompanyService(
        ILogger<CompanyService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Company>(logger, cache, mapper, contextFactory)
    {
        public override Task<Company> UpdateAsync(Company entity)
        {
            throw new NotImplementedException();
        }
    }
}
