using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class PlaySessionService(
        ILogger<PlaySessionService> logger,
        DatabaseContext dbContext,
        GameClient gameClient) : BaseDatabaseService<PlaySession>(dbContext, logger)
    {
        public async Task<PlaySession> GetLatestSession(Guid gameId, Guid userId)
        {
            return await Query(ps => ps.GameId == gameId && ps.UserId == userId).OrderByDescending(ps => ps.End).FirstOrDefaultAsync();
        }

        public async Task StartSession(Guid gameId, Guid userId)
        {
            using (var op = Logger.BeginOperation("Starting game session"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("UserId", userId);
                
                try
                {
                    var existingSession = Query(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

                    if (existingSession != null)
                        await DeleteAsync(existingSession);

                    var session = new PlaySession()
                    {
                        GameId = gameId,
                        UserId = userId,
                        Start = DateTime.UtcNow
                    };

                    await AddAsync(session);
                
                    await gameClient.StartedAsync(gameId);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to start session recording for game with ID {GameId}", gameId);
                }
                
                op.Complete();
            }
        }

        public async Task EndSession(Guid gameId, Guid userId)
        {
            using (var op = Logger.BeginOperation("Ending game session"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("UserId", userId);
                
                try
                {
                    var existingSession = Query(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

                    if (existingSession != null)
                    {
                        existingSession.End = DateTime.UtcNow;

                        await UpdateAsync(existingSession);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to end session recording for game with ID {GameId}", gameId);
                }
                finally
                {
                    await gameClient.StoppedAsync(gameId);
                }
                
                op.Complete();
            }
        }
    }
}
