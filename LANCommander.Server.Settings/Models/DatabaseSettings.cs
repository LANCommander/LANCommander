using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class DatabaseSettings
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Unknown;
    public string ConnectionString { get; set; } = String.Empty;
}