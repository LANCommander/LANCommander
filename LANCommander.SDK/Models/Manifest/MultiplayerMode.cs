using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class MultiplayerMode : BaseModel
    {
        public MultiplayerType Type { get; set; }
        public NetworkProtocol NetworkProtocol { get; set; }
        public string Description { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int Spectators { get; set; }
    }
}
