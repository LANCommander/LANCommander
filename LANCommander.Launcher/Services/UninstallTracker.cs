using System;
using System.Collections.Generic;

namespace LANCommander.Launcher.Services;

/// <summary>
/// The games being uninstalled right now. Uninstalls start from whichever action bar was used (the
/// detail page's, or a throwaway one behind a library context menu), so every bar showing the game
/// follows this rather than its own flag.
/// </summary>
public static class UninstallTracker
{
    private static readonly HashSet<Guid> Uninstalling = new();
    private static readonly object Lock = new();

    /// <summary>Raised with the game's id and whether it is now being uninstalled. May be raised off the UI thread.</summary>
    public static event Action<Guid, bool>? Changed;

    /// <summary>Raised with the game's id once its files are gone and it is marked uninstalled.</summary>
    public static event Action<Guid>? Uninstalled;

    public static bool IsUninstalling(Guid gameId)
    {
        lock (Lock)
            return Uninstalling.Contains(gameId);
    }

    /// <summary>Marks the game as uninstalling; false when it already was.</summary>
    public static bool TryBegin(Guid gameId)
    {
        lock (Lock)
            if (!Uninstalling.Add(gameId))
                return false;

        Changed?.Invoke(gameId, true);

        return true;
    }

    public static void End(Guid gameId, bool succeeded)
    {
        lock (Lock)
            Uninstalling.Remove(gameId);

        if (succeeded)
            Uninstalled?.Invoke(gameId);

        Changed?.Invoke(gameId, false);
    }
}
