namespace LANCommander.Steam.Enums;

/// <summary>
/// Status of an installation job
/// </summary>
public enum SteamCmdInstallStatus
{
    /// <summary>
    /// Job is queued and waiting to start
    /// </summary>
    Queued,

    /// <summary>
    /// Job is currently being processed
    /// </summary>
    InProgress,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled
    /// </summary>
    Cancelled
}
