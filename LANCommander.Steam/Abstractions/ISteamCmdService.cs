using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Enums;
using LANCommander.Steam.Events;
using LANCommander.Steam.Models;

namespace LANCommander.Steam.Abstractions;

/// <summary>
/// Interface for SteamCMD service operations
/// </summary>
public interface ISteamCmdService
{
    /// <summary>
    /// Get or set the SteamCMD executable path
    /// </summary>
    string? ExecutablePath { get; set; }

    /// <summary>
    /// Get app info (changenumber / buildid and time updated) for the public branch via app_info_print.
    /// Requires SteamCMD to be installed and configured. Returns null if SteamCMD is unavailable or the app cannot be queried.
    /// </summary>
    Task<SteamCmdAppInfo?> GetAppInfoAsync(uint appId, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when an install job status changes (started, completed, failed)
    /// </summary>
    event EventHandler<SteamCmdInstallStatusEventArgs>? InstallStatusChanged;

    /// <summary>
    /// Event fired when install progress is updated
    /// </summary>
    event EventHandler<SteamCmdInstallProgressEventArgs>? InstallProgress;

    /// <summary>
    /// Check the connection status for a username
    /// </summary>
    Task<SteamCmdConnectionStatus> GetConnectionStatusAsync(string username);

    /// <summary>
    /// Auto-detect the SteamCMD executable path
    /// </summary>
    Task<string> AutoDetectSteamCmdPathAsync();

    /// <summary>
    /// Login to Steam with username and optional password
    /// </summary>
    Task<SteamCmdStatus> LoginToSteamAsync(string username, string? password = null);

    /// <summary>
    /// Logout from Steam
    /// </summary>
    Task<SteamCmdStatus> LogoutAsync(string username);

    /// <summary>
    /// Queue an installation job for Steam content
    /// Returns a job ID that can be used to track progress
    /// </summary>
    Task InstallContentAsync(uint appId, string installDirectory, string? username = null);

    /// <summary>
    /// Remove installed content from a directory
    /// </summary>
    Task<SteamCmdStatus> RemoveContentAsync(string installDirectory);

    /// <summary>
    /// Get all profiles (requires profile store to be configured)
    /// </summary>
    Task<IEnumerable<SteamCmdProfile>> GetProfilesAsync();

    /// <summary>
    /// Get a profile by username (requires profile store to be configured)
    /// </summary>
    Task<SteamCmdProfile?> GetProfileAsync(string username);

    /// <summary>
    /// Save a profile (requires profile store to be configured)
    /// </summary>
    Task SaveProfileAsync(SteamCmdProfile profile);

    /// <summary>
    /// Delete a profile by username (requires profile store to be configured)
    /// </summary>
    Task DeleteProfileAsync(string username);
}
