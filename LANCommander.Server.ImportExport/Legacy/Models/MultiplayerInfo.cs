using LANCommander.Server.ImportExport.Legacy.Enums;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class MultiplayerInfo
{
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string Description { get; set; }
    public NetworkProtocol NetworkProtocol { get; set; }
}