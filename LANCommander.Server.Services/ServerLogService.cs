using CoreRCON;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ServerConsoleService(
        ILogger<ServerConsoleService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<ServerConsole>(logger, cache, mapper, contextFactory)
    {
        public override Task<ServerConsole> UpdateAsync(ServerConsole entity)
        {
            throw new NotImplementedException();
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
