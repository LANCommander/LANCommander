using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class CategoryService(
        ILogger<CategoryService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<Category>(logger, cache, mapper, context)
    {
        public override Task<Category> UpdateAsync(Category entity)
        {
            throw new NotImplementedException();
        }
    }
}
