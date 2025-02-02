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
        public override async Task<Category> UpdateAsync(Category entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.Children);
                await context.UpdateRelationshipAsync(c => c.Games);
                await context.UpdateRelationshipAsync(c => c.Parent);
            });
        }
    }
}
