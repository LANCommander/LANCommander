using LANCommander.Server.Services;
using LANCommander.Server.Services.MatchStatistics;

namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    public async Task GameServerConnectedAsync(Guid gameId, Guid serverId)
    {
        try
        {
            if (!Guid.TryParse(Context.UserIdentifier, out var userId))
                return;

            using var scope = scopeFactory.CreateScope();

            var playSessionService = scope.ServiceProvider.GetRequiredService<PlaySessionService>();

            if (!await playSessionService.SetSessionServerAsync(gameId, userId, serverId))
                return;

            // Participants already reported on that server may now be linkable to the player
            matchNotificationDispatcher.Post($"linking players on server {serverId}", (services, cancellationToken) =>
                services.GetRequiredService<PlayerIdentityService>().ResolveOpenMatchesAsync(gameId, serverId, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record server {ServerId} for game {GameId}", serverId, gameId);
        }
    }
}
