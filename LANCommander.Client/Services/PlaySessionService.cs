using JetBrains.Annotations;
using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class PlaySessionService : BaseDatabaseService<PlaySession>
    {
        public PlaySessionService(DatabaseContext dbContext) : base(dbContext)
        {
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
