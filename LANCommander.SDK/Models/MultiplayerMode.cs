using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class MultiplayerMode : KeyedModel
    {
        public MultiplayerType Type { get; set; }
        public NetworkProtocol NetworkProtocol { get; set; }
        public string Description { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int Spectators { get; set; }
    }
}
