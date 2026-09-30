using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Game = LANCommander.Launcher.Data.Models.Game;

namespace LANCommander.Launcher.Services
{
    /// <summary>
    /// Queues updates for installed games that have automatic updates on, as soon as the server reports a
    /// newer version with an archive. Games with automatic updates off stay on their installed version.
    /// </summary>
    public class AutoUpdateService(
        ILogger<AutoUpdateService> logger,
        GameService gameService,
        GameClient gameClient,
        InstallService installService) : BaseService(logger)
    {
        /// <summary>Checks every installed game that takes updates and queues the ones with an update.</summary>
        public async Task CheckAllAsync(CancellationToken cancellationToken = default)
        {
            var games = await gameService
                .Query(g => g.Installed && g.AutoUpdate)
                .ToListAsync(cancellationToken);

            foreach (var game in games)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    await CheckAndQueueAsync(game);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Could not check game {GameTitle} ({GameId}) for updates", game.Title, game.Id);
                }
            }
        }

        /// <summary>
        /// Asks the server whether the game has an update and queues it when automatic updates are on.
        /// Returns whether an update is available.
        /// </summary>
        public async Task<bool> CheckAndQueueAsync(Game game)
        {
            if (!game.Installed || !game.AutoUpdate || string.IsNullOrWhiteSpace(game.InstallDirectory))
                return false;

            var hasUpdate = await gameClient.CheckForUpdateAsync(game.Id, game.InstalledVersion, game.InstalledVersionId);

            if (hasUpdate)
                await QueueAsync(game);

            return hasUpdate;
        }

        /// <summary>Queues an update for a game that takes updates, unless it's already in the queue.</summary>
        public async Task QueueAsync(Game game)
        {
            if (!game.AutoUpdate || installService.IsActive(game.Id))
                return;

            Logger?.LogInformation("Automatically queuing update for {GameTitle} ({GameId})", game.Title, game.Id);

            await installService.Add(game, game.InstallDirectory);
        }
    }
}
