using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Helpers;
using LANCommander.Models;

namespace LANCommander.Services
{
    public class PlaySessionService : BaseDatabaseService<PlaySession>
    {
        public PlaySessionService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor) { }

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
        }

        public async Task EndSession(Guid gameId, Guid userId)
        {
            var existingSession = Get(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await Update(existingSession);
            }
        }
    }
}
