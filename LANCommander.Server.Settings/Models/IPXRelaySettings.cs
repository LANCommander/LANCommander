namespace LANCommander.Server.Settings.Models;

public class IPXRelaySettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = String.Empty;
    public int Port { get; set; } = 213;
    public bool Logging { get; set; } = false;
}