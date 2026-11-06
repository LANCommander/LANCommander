using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

/// <summary>
/// Who is playing what on the server, over the RPC hub. The server pushes every change, so
/// <see cref="GetAsync"/> is needed only to catch up after (re)connecting: see
/// <see cref="Connected"/>.
/// </summary>
/// <remarks>
/// A server older than presence has none of these hub methods. Every call then fails
/// quietly: <see cref="GetAsync"/> returns nothing and <see cref="GetVisibilityAsync"/> null.
/// </remarks>
public class PresenceClient(ILogger<PresenceClient> logger)
{
    /// <summary>A player started a game, or what they are doing in it changed. Raised off the UI thread.</summary>
    public event EventHandler<UserPresence> PresenceChanged;

    /// <summary>A player left their game or hid it; the argument is their user id. Raised off the UI thread.</summary>
    public event EventHandler<Guid> PresenceEnded;

    /// <summary>The RPC connection opened: fetch everyone's presence again.</summary>
    public event EventHandler Connected;

    /// <summary>The RPC connection closed: what is known may be stale.</summary>
    public event EventHandler Disconnected;

    /// <summary>Everyone in a game whose visibility lets this user see them.</summary>
    public async Task<IReadOnlyList<UserPresence>> GetAsync()
    {
        if (RpcClient.Hub == null)
            return [];

        try
        {
            var presence = await RpcClient.Hub.GetPresenceAsync();

            return presence?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not get presence from the server");
            return [];
        }
    }

    /// <summary>
    /// Reports what this user is doing in a game they are playing, or clears it back to just
    /// the game with null.
    /// </summary>
    public async Task UpdateAsync(Guid gameId, PresenceActivity activity)
    {
        if (RpcClient.Hub == null)
            return;

        try
        {
            await RpcClient.Hub.UpdatePresenceAsync(gameId, activity);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not report presence for game {GameId}", gameId);
        }
    }

    /// <summary>This user's visibility, or null when the server can't say.</summary>
    public async Task<PresenceVisibility?> GetVisibilityAsync()
    {
        if (RpcClient.Hub == null)
            return null;

        try
        {
            return await RpcClient.Hub.GetPresenceVisibilityAsync();
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not get presence visibility");
            return null;
        }
    }

    /// <summary>Sets this user's visibility. False when the server didn't take it.</summary>
    public async Task<bool> SetVisibilityAsync(PresenceVisibility visibility)
    {
        if (RpcClient.Hub == null)
            return false;

        try
        {
            await RpcClient.Hub.SetPresenceVisibilityAsync(visibility);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not set presence visibility to {Visibility}", visibility);
            return false;
        }
    }

    internal void OnPresenceChanged(UserPresence presence)
    {
        if (presence != null)
            PresenceChanged?.Invoke(this, presence);
    }

    internal void OnPresenceEnded(Guid userId) => PresenceEnded?.Invoke(this, userId);

    internal void OnConnected() => Connected?.Invoke(this, EventArgs.Empty);

    internal void OnDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);
}
