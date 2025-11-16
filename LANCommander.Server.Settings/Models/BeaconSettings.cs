namespace LANCommander.Server.Settings.Models;

public class BeaconSettings
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "LANCommander";
    public string Address { get; set; } = String.Empty;
    public int Port { get; set; } = 35891;
}