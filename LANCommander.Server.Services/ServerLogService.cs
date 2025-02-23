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
    public sealed class ServerConsoleService(
        ILogger<ServerConsoleService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<ServerConsole>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<ServerConsole> AddAsync(ServerConsole entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sc => sc.Server);
            });
        }

        public override async Task<ServerConsole> UpdateAsync(ServerConsole entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sc => sc.Server);
            });
        }
        
        public async Task<string[]> ReadLogAsync(Guid logId)
        {
            var log = await GetAsync(logId);

            if (log.Type != ServerConsoleType.LogFile)
                throw new Exception("Invalid console type");

            var logPath = Path.Combine(log.Server.WorkingDirectory, log.Path);

            return await File.ReadAllLinesAsync(logPath);
        }
    }
}
