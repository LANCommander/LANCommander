namespace LANCommander.Launcher.Models;

public class ConnectionState
{
    public bool ValidCredentials { get; set; }
    public bool IsConnected { get; set; }
    public bool OfflineModeEnabled { get; set; }
    public bool IsStartup { get; set; }
}