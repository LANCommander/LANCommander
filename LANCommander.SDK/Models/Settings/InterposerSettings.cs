using System.Collections.Generic;

namespace LANCommander.SDK.Models;

/// <summary>
/// The launcher's use of the LANCommander Interposer it bundles. Windows only.
/// </summary>
public class InterposerSettings
{
    /// <summary>
    /// Whether the launcher may inject its own copy of the Interposer into a game that does not have
    /// the Interposer redistributable, when a setting calls for it. Games that have the redistributable
    /// still get the player's settings either way.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Values for every game, as dotted Interposer option keys (<c>RichPresence.Discord.Enabled</c>).
    /// They sit over the server's values and under each game's own.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();
}
