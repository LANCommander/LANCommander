using System;

namespace LANCommander.Steam.Models;

/// <summary>
/// App info from SteamCMD (app_info_print): changenumber (buildid) and last updated time for the public branch.
/// </summary>
public class SteamCmdAppInfo
{
    /// <summary>
    /// Steam application ID.
    /// </summary>
    public uint AppId { get; set; }

    /// <summary>
    /// Build ID / changenumber for the public branch.
    /// </summary>
    public string? Changenumber { get; set; }

    /// <summary>
    /// When the public branch was last updated (Unix timestamp from SteamCMD).
    /// </summary>
    public DateTimeOffset? TimeUpdated { get; set; }
}
