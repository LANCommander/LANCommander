using AutoMapper;
using CoreRCON;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class ServerConsoleService : BaseDatabaseService<ServerConsole>
    {
        public ServerConsoleService(
            ILogger<ServerConsoleService> logger,
            IFusionCache cache,
            IMapper mapper,
            DatabaseContext databaseContext) : base(logger, cache, databaseContext, mapper) { }

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
