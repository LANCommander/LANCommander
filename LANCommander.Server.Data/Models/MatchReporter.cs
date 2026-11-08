using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// Something that sent a match's events: a dedicated server, a player's client or a plugin. Each
    /// reporter numbers its own events, which is how gaps are detected.
    /// </summary>
    [Table("MatchReporters")]
    public class MatchReporter : BaseModel
    {
        public Guid MatchId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(MatchId))]
        [InverseProperty(nameof(Models.Match.Reporters))]
        public Match? Match { get; set; }

        public MatchReporterKind Kind { get; set; }

        public MatchReporterTrust Trust { get; set; }

        /// <summary>The user the reporter authenticated as, if it isn't a plugin.</summary>
        public Guid? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>The plugin that reported, for <see cref="MatchReporterKind.Plugin"/>.</summary>
        [MaxLength(191)]
        public string? PluginId { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        /// <summary>The highest sequence number written for this reporter.</summary>
        public long LastSequence { get; set; }

        /// <summary>How many sequence numbers were skipped. Late arrivals don't reduce it.</summary>
        public long GapCount { get; set; }

        public ICollection<MatchEvent>? Events { get; set; }
        public ICollection<MatchAttachment>? Attachments { get; set; }
    }
}
