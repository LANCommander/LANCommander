using CoreRCON;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace LANCommander.Server.Services
{
    public class ServerConsoleService : BaseDatabaseService<ServerConsole>
    {
        public ServerConsoleService(
            ILogger<ServerConsoleService> logger,
            Repository<ServerConsole> repository) : base(logger, repository) { }

        public async Task<string[]> ReadLog(Guid logId)
        {
            var log = await Get(logId);

            if (log.Type != ServerConsoleType.LogFile)
                throw new Exception("Invalid console type");

            var logPath = Path.Combine(log.Server.WorkingDirectory, log.Path);

            return await File.ReadAllLinesAsync(logPath);
        }
    }
}
