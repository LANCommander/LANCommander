using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ServerService(
        ILogger<ServerService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        GameService gameService,
        StorageLocationService storageLocationService,
        ImportService importService) : BaseDatabaseService<Data.Models.Server>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Data.Models.Server> UpdateAsync(Data.Models.Server entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Actions);
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.HttpPaths);
                await context.UpdateRelationshipAsync(s => s.Pages);
                await context.UpdateRelationshipAsync(s => s.Scripts);
                await context.UpdateRelationshipAsync(s => s.ServerConsoles);
            });
        }

        public async Task<Data.Models.Server> ImportAsync(Guid objectKey)
        {
            return await importService.ImportServerAsync(objectKey);
        }
    }
}
