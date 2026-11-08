using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A number the game reported for a player or team at the end of a match: kills, resources
    /// collected, best lap. Rates and averages are never stored; they are worked out when queried.
    /// </summary>
    /// <remarks>
    /// Kept as one row per value, rather than JSON, so leaderboards can be queried the same way on every
    /// database. Not a <see cref="BaseModel"/>, for the same reason as <see cref="MatchEvent"/>.
    /// </remarks>
    [Table("MatchStatistics")]
    [Index(nameof(Key), nameof(MatchId))]
    [Index(nameof(MatchId), nameof(Key))]
    public class MatchStatistic
    {
        [Key]
        public Guid Id { get; set; }

        public Guid MatchId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(MatchId))]
        [InverseProperty(nameof(Models.Match.Statistics))]
        public Match? Match { get; set; }

        /// <summary>The player the value is for. Null for a team's value.</summary>
        public Guid? ParticipantId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ParticipantId))]
        [InverseProperty(nameof(MatchParticipant.Statistics))]
        public MatchParticipant? Participant { get; set; }

        [MaxLength(64)]
        public string? Team { get; set; }

        /// <summary>What was counted: <c>kills</c>, <c>resources_collected</c>, <c>best_lap_ms</c>.</summary>
        [MaxLength(64)]
        public string Key { get; set; } = "";

        public double Value { get; set; }
    }
}
