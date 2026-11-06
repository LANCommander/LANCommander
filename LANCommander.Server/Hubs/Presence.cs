using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    public async Task UpdatePresenceAsync(Guid gameId, PresenceActivity activity)
    {
        try
        {
            if (Guid.TryParse(Context.UserIdentifier, out var userId))
                await presenceService.UpdateActivityAsync(userId, gameId, activity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record presence for game {GameId}", gameId);
        }
    }

    public async Task<IEnumerable<UserPresence>> GetPresenceAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            return [];

        return await presenceService.GetAllAsync(userId);
    }

    public async Task<PresenceVisibility> GetPresenceVisibilityAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            return PresenceVisibility.Visible;

        return await presenceService.GetVisibilityAsync(userId);
    }

    public async Task SetPresenceVisibilityAsync(PresenceVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
            throw new HubException($"Unknown visibility {visibility}");

        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            await presenceService.SetVisibilityAsync(userId, visibility);
    }
}
