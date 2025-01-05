using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services
{
    public class PlaySessionService : BaseDatabaseService<PlaySession>
    {
        private ServerService ServerService { get; set; }
        private ServerProcessService ServerProcessService;

        public PlaySessionService(
            ILogger<PlaySessionService> logger,
            DatabaseContext dbContext,
            ServerService serverService,
            ServerProcessService serverProcessService) : base(logger, dbContext)
        {
            ServerService = serverService;
            ServerProcessService = serverProcessService;
        }

        public async Task StartSession(Guid gameId, Guid userId)
        {
            var existingSession = Get(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

            if (existingSession != null)
                await Delete(existingSession);

            var session = new PlaySession()
            {
                GameId = gameId,
                UserId = userId,
                Start = DateTime.UtcNow
            };

            await Add(session);

            var servers = ServerService.Get(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

            foreach (var server in servers)
            {
                ServerProcessService.StartServerAsync(server.Id);
            }
        }

        public async Task EndSession(Guid gameId, Guid userId)
        {
            var existingSession = Get(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await Update(existingSession);
            }

            var activeSessions = await Get(ps => ps.GameId == gameId && ps.End == null).AnyAsync();

            if (!activeSessions)
            {
                var servers = ServerService.Get(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

                foreach (var server in servers)
                {
                    ServerProcessService.StopServer(server.Id);
                }
            }
        }
    }
}
