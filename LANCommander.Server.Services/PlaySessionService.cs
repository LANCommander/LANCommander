using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class PlaySessionService : BaseDatabaseService<PlaySession>
    {
        private ServerService ServerService { get; set; }
        private ServerProcessService ServerProcessService;

        public PlaySessionService(
            ILogger<PlaySessionService> logger,
            IFusionCache cache,
            Repository<PlaySession> repository,
            ServerService serverService,
            ServerProcessService serverProcessService) : base(logger, cache, repository)
        {
            ServerService = serverService;
            ServerProcessService = serverProcessService;
        }

        public async Task StartSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
                await DeleteAsync(existingSession);

            var session = new PlaySession()
            {
                GameId = gameId,
                UserId = userId,
                Start = DateTime.UtcNow
            };

            await AddAsync(session);

            var servers = await ServerService.GetAsync(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

            foreach (var server in servers)
            {
                ServerProcessService.StartServerAsync(server.Id);
            }
        }

        public async Task EndSessionAsync(Guid gameId, Guid userId)
        {
            var existingSession = await FirstOrDefaultAsync(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null);

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await UpdateAsync(existingSession);
            }

            var activeSessions = (await GetAsync(ps => ps.GameId == gameId && ps.End == null)).Any();

            if (!activeSessions)
            {
                var servers = await ServerService.GetAsync(s => s.GameId == gameId && s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnPlayerActivity);

                foreach (var server in servers)
                {
                    ServerProcessService.StopServerAsync(server.Id);
                }
            }
        }
    }
}
