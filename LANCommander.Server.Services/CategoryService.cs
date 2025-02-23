using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class CategoryService(
        ILogger<CategoryService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<Category>(logger, cache, mapper, httpContextAccessor, context)
    {
        public override async Task<Category> AddAsync(Category entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(c => c.Children);
                await context.UpdateRelationshipAsync(c => c.Games);
                await context.UpdateRelationshipAsync(c => c.Parent);
            });
        }

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
