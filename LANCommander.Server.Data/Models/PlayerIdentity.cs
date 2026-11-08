using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// Links an id a game uses for a player, such as a Steam ID or an in-game name, to a LANCommander
    /// user, so match participants reported under that id are linked to the user.
    /// </summary>
    [Table("PlayerIdentities")]
    [Index(nameof(Kind), nameof(Value))]
    [Index(nameof(GameId), nameof(Kind), nameof(Value), IsUnique = true)]
    public class PlayerIdentity : BaseModel
    {
        public Guid UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty(nameof(Models.User.PlayerIdentities))]
        public User? User { get; set; }

        /// <summary>The game the id is used in. Null when it holds for every game, as a Steam ID does.</summary>
        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty(nameof(Models.Game.PlayerIdentities))]
        public Game? Game { get; set; }

        /// <summary>The prefix of a <see cref="MatchParticipant.PlayerKey"/>: <c>steam</c>, <c>name</c>.</summary>
        [MaxLength(32)]
        public string Kind { get; set; } = "";

        /// <summary>
        /// The rest of the key. Names are stored lowercased, so they match whatever case the game reports.
        /// </summary>
        [MaxLength(158)]
        public string Value { get; set; } = "";

        public PlayerIdentitySource Source { get; set; }
    }
}
