namespace LANCommander.Launcher.Settings;

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "Data Source=LANCommander.db;Cache=Shared";
    public string BackupsPath { get; set; } = "Backups";
}