namespace LANCommander.SDK.Models;

/// <summary>
/// Whether games with LAN multiplayer join the server's network relay, when the server runs one,
/// so players on other networks see them as if they shared a LAN. Windows only.
/// </summary>
public class NetworkRelaySettings
{
    public bool Enabled { get; set; } = true;
}
