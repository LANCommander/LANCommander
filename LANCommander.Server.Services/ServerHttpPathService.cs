using CoreRCON;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ServerHttpPathService(
        ILogger<ServerHttpPathService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<ServerHttpPath>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<ServerHttpPath> AddAsync(ServerHttpPath entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sc => sc.Server);
            });
        }

        public override async Task<ServerHttpPath> UpdateAsync(ServerHttpPath entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sc => sc.Server);
            });
        }
    }
}
