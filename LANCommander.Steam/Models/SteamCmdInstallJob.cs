using System;
using System.Threading.Tasks;
using LANCommander.Steam.Enums;

namespace LANCommander.Steam.Models;

/// <summary>
/// Represents an installation job in the queue
/// </summary>
public class SteamCmdInstallJob
{
    /// <summary>
    /// Unique identifier for this install job
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Steam App ID to install
    /// </summary>
    public uint AppId { get; set; }

    /// <summary>
    /// Installation directory
    /// </summary>
    public string InstallDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Username for Steam login (null for anonymous)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Current status of the installation
    /// </summary>
    public SteamCmdInstallStatus Status { get; set; } = SteamCmdInstallStatus.Queued;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Current status message
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Bytes downloaded
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    /// Total bytes to download
    /// </summary>
    public long BytesTotal { get; set; }

    /// <summary>
    /// Download speed in bytes per second
    /// </summary>
    public long BytesPerSecond { get; set; }

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the job completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Task completion source for awaiting the job
    /// </summary>
    internal TaskCompletionSource<SteamCmdStatus>? CompletionSource { get; set; }
}
