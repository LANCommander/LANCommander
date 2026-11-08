using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A player's or team's place in a match's final standings, as the game decided them, including
    /// tie-breaks and penalties.
    /// </summary>
    [Table("MatchResults")]
    public class MatchResult : BaseModel
    {
        public Guid MatchId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(MatchId))]
        [InverseProperty(nameof(Models.Match.Results))]
        public Match? Match { get; set; }

        /// <summary>The player the result is for. Null for a team's result.</summary>
        public Guid? ParticipantId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ParticipantId))]
        [InverseProperty(nameof(MatchParticipant.Results))]
        public MatchParticipant? Participant { get; set; }

        /// <summary>The team the result is for, or the player's team.</summary>
        [MaxLength(64)]
        public string? Team { get; set; }

        /// <summary>1 for the winner. Tied entries share a rank.</summary>
        public int Rank { get; set; }

        public double? Score { get; set; }

        public MatchResultStatus Status { get; set; } = MatchResultStatus.Finished;

        /// <summary>A finishing time in milliseconds, for races.</summary>
        public long? TimeMilliseconds { get; set; }

        /// <summary>Anything else the game reports for the entry, as a JSON object.</summary>
        public string? Payload { get; set; }
    }
}
