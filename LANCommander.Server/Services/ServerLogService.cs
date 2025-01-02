using CoreRCON;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using System.Diagnostics;
using System.Net;

namespace LANCommander.Server.Services
{
    public class ServerConsoleService : BaseDatabaseService<ServerConsole>
    {
        public ServerConsoleService(
            ILogger<ServerConsoleService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }

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
