#if DEBUG
using System;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Fixtures;

/// <summary>Players shown as in a game, for fixtures, which have no server to hear it from.</summary>
public static class FixturePresence
{
    /// <summary>
    /// Shows <paramref name="name"/> playing <paramref name="game"/> since <paramref name="minutes"/>
    /// before <see cref="FixtureContext.Now"/>, through the directory every view reads.
    /// </summary>
    public static PlayerPresenceViewModel Playing(
        Guid userId, string name, FixtureGame game, int minutes, PresenceActivity? activity = null, bool withIcon = true)
    {
        var now = new DateTimeOffset(FixtureContext.Now);

        return PresenceDirectory.Current!.SeedFixture(new UserPresence
        {
            UserId = userId,
            UserName = name,
            GameId = game.Id,
            GameTitle = game.Title,
            StartedOn = now.UtcDateTime.AddMinutes(-minutes),
            Activity = activity,
        }, withIcon ? game.IconPath : null, now);
    }

    /// <summary>What a match in progress reports through the Interposer.</summary>
    public static PresenceActivity InAMatch => new()
    {
        Details = "ONS-Torlan",
        State = "Red 3 - 2 Blue",
        PartySize = 6,
        PartyMax = 16,
    };
}
#endif
