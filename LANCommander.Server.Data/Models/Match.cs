using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// One played multiplayer session of a game: a race, a round set, a skirmish. Every event, result
    /// and statistic is scoped to a match.
    /// </summary>
    [Table("Matches")]
    [Index(nameof(GameId), nameof(ExternalId), IsUnique = true)]
    [Index(nameof(GameId), nameof(StartedOn))]
    [Index(nameof(Status), nameof(LastEventOn))]
    public class Match : BaseModel
    {
        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty(nameof(Models.Game.Matches))]
        public Game? Game { get; set; }

        /// <summary>The game server the match was played on, when LANCommander manages it.</summary>
        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty(nameof(Models.Server.Matches))]
        public Server? Server { get; set; }

        /// <summary>
        /// The reporter's own id for the match, so creating it again returns the same match. Unique per game.
        /// </summary>
        [MaxLength(191)]
        public string? ExternalId { get; set; }

        [MaxLength(255)]
        public string? Map { get; set; }

        [MaxLength(255)]
        public string? Mode { get; set; }

        [MaxLength(64)]
        public string? GameVersion { get; set; }

        /// <summary>For servers LANCommander doesn't manage: the name the server goes by.</summary>
        [MaxLength(255)]
        public string? ServerName { get; set; }

        /// <summary>For servers LANCommander doesn't manage: the address players connected to.</summary>
        [MaxLength(255)]
        public string? ServerAddress { get; set; }

        public MatchStatus Status { get; set; } = MatchStatus.Open;

        public DateTime? StartedOn { get; set; }
        public DateTime? EndedOn { get; set; }

        [MaxLength(64)]
        public string? EndReason { get; set; }

        /// <summary>The rules the match was played with (time limit, cvars, ...), as a JSON object.</summary>
        public string? Settings { get; set; }

        /// <summary>Anything else the reporter wants kept with the match, as a JSON object.</summary>
        public string? Metadata { get; set; }

        /// <summary>When the last event was written. Used to abandon matches that stop reporting.</summary>
        public DateTime? LastEventOn { get; set; }

        public ICollection<MatchReporter>? Reporters { get; set; }
        public ICollection<MatchParticipant>? Participants { get; set; }
        public ICollection<MatchEvent>? Events { get; set; }
        public ICollection<MatchResult>? Results { get; set; }
        public ICollection<MatchStatistic>? Statistics { get; set; }
        public ICollection<MatchAttachment>? Attachments { get; set; }
    }
}
