using System;
using LANCommander.Steam.Models;

namespace LANCommander.Steam.Events;

/// <summary>
/// Event arguments for install progress updates
/// </summary>
public class SteamCmdInstallProgressEventArgs : EventArgs
{
    /// <summary>
    /// The install job this progress update is for
    /// </summary>
    public SteamCmdInstallJob Job { get; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress { get; }

    /// <summary>
    /// Status message
    /// </summary>
    public string StatusMessage { get; }

    /// <summary>
    /// Bytes downloaded
    /// </summary>
    public long BytesDownloaded { get; }

    /// <summary>
    /// Total bytes to download
    /// </summary>
    public long BytesTotal { get; }

    /// <summary>
    /// Download speed in bytes per second
    /// </summary>
    public long BytesPerSecond { get; }

    public SteamCmdInstallProgressEventArgs(
        SteamCmdInstallJob job,
        double progress,
        string statusMessage,
        long bytesDownloaded = 0,
        long bytesTotal = 0,
        long bytesPerSecond = 0)
    {
        Job = job;
        Progress = progress;
        StatusMessage = statusMessage;
        BytesDownloaded = bytesDownloaded;
        BytesTotal = bytesTotal;
        BytesPerSecond = bytesPerSecond;
    }
}
