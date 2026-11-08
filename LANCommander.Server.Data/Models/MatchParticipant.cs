using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A player, bot or team member who took part in a match, and the LANCommander user they are, when
    /// known.
    /// </summary>
    [Table("MatchParticipants")]
    [Index(nameof(MatchId), nameof(PlayerKey), IsUnique = true)]
    [Index(nameof(UserId))]
    public class MatchParticipant : BaseModel
    {
        public Guid MatchId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(MatchId))]
        [InverseProperty(nameof(Models.Match.Participants))]
        public Match? Match { get; set; }

        /// <summary>
        /// The game's id for the player, prefixed by its kind: <c>steam:STEAM_0:1:123</c>,
        /// <c>name:Pat</c>, <c>bot:3</c>. Events refer to participants by this key.
        /// </summary>
        [MaxLength(191)]
        public string PlayerKey { get; set; } = "";

        [MaxLength(255)]
        public string? DisplayName { get; set; }

        [MaxLength(64)]
        public string? Team { get; set; }

        public Guid? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty(nameof(Models.User.MatchParticipants))]
        public User? User { get; set; }

        public MatchParticipantLinkSource LinkSource { get; set; } = MatchParticipantLinkSource.None;

        /// <summary>Milliseconds of game time when the participant joined.</summary>
        public long? JoinedAtMilliseconds { get; set; }

        /// <summary>Milliseconds of game time when the participant left, if they left early.</summary>
        public long? LeftAtMilliseconds { get; set; }

        public ICollection<MatchResult>? Results { get; set; }
        public ICollection<MatchStatistic>? Statistics { get; set; }
    }
}
