namespace LANCommander.SDK.Models;

public class GameSettings
{
    public string[] InstallDirectories { get; set; } = [];

    /// <summary>
    /// Number of times to attempt downloading and extracting a game before giving up.
    /// </summary>
    public int MaxInstallAttempts { get; set; } = 10;
}