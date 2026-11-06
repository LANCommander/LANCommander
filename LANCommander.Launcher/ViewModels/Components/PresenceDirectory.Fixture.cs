#if DEBUG
using System;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.ViewModels.Components;

public sealed partial class PresenceDirectory
{
    /// <summary>
    /// Shows <paramref name="presence"/> for a visual fixture, which has no server to hear it from,
    /// with the icon from a local file and the timer stopped at <paramref name="now"/>.
    /// </summary>
    internal PlayerPresenceViewModel SeedFixture(UserPresence presence, string? iconPath, DateTimeOffset now)
    {
        _timer.Stop();

        var player = For(presence.UserId);

        player.Presence = presence;
        player.IconUrl = iconPath;
        player.Tick(now);

        return player;
    }
}
#endif
