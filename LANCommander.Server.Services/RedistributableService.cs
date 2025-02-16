using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class RedistributableService(
        ILogger<SDK.Services.RedistributableService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        ImportService importService) : BaseDatabaseService<Redistributable>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Redistributable> UpdateAsync(Redistributable entity)
        {
            if (entity.Games != null && entity.Games.Any())
            {
                foreach (var game in entity.Games)
                {
                    await cache.ExpireGameCacheAsync(game?.Id);
                }
            }
            
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(r => r.Archives);
                await context.UpdateRelationshipAsync(r => r.Games);
                await context.UpdateRelationshipAsync(r => r.Pages);
                await context.UpdateRelationshipAsync(r => r.Scripts);
            });
        }

        public async Task<Redistributable> ImportAsync(Guid objectKey)
        {
            return await importService.ImportRedistributableAsync(objectKey);
        }
    }
}
