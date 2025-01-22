﻿using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("MultiplayerModes")]
    public class MultiplayerMode : BaseModel
    {
        public MultiplayerType Type { get; set; }
        public NetworkProtocol NetworkProtocol { get; set; }
        public string? Description { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int Spectators { get; set; }
        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("MultiplayerModes")]
        public Game? Game { get; set; }
    }
}
