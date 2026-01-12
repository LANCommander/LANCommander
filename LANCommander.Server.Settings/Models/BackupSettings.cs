using LANCommander.SDK;

namespace LANCommander.Server.Settings.Models;

public class BackupSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Backups");
}