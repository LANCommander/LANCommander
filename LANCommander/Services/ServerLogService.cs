using CoreRCON;
using LANCommander.Data;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using System.Diagnostics;
using System.Net;

namespace LANCommander.Services
{
    public class ServerConsoleService : BaseDatabaseService<ServerConsole>
    {
        public ServerConsoleService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor) { }

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
