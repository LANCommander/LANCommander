using System;
using LANCommander.Steam.Enums;
using LANCommander.Steam.Models;

namespace LANCommander.Steam.Events;

/// <summary>
/// Event arguments for install status changes (started, completed, failed)
/// </summary>
public class SteamCmdInstallStatusEventArgs : EventArgs
{
    /// <summary>
    /// The install job this status change is for
    /// </summary>
    public SteamCmdInstallJob Job { get; }

    /// <summary>
    /// The new status
    /// </summary>
    public SteamCmdInstallStatus Status { get; }

    /// <summary>
    /// Error message if the status is Failed
    /// </summary>
    public string? ErrorMessage { get; }

    public SteamCmdInstallStatusEventArgs(SteamCmdInstallJob job, SteamCmdInstallStatus status, string? errorMessage = null)
    {
        Job = job;
        Status = status;
        ErrorMessage = errorMessage;
    }
}
