namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    public async Task GameKeepAliveAsync(Guid gameId)
    {
        try
        {
            if (Guid.TryParse(Context.UserIdentifier, out var userId))
                await keepAliveTracker.TouchAsync(gameId, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record keepalive for game {GameId}", gameId);
        }
    }
}
