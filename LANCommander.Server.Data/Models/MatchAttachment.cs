using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A file kept with a match, such as a demo, a replay or a server log. The file is stored under its
    /// storage location by match and attachment id; <see cref="FileName"/> is only what it was called.
    /// </summary>
    [Table("MatchAttachments")]
    public class MatchAttachment : BaseModel
    {
        public Guid MatchId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(MatchId))]
        [InverseProperty(nameof(Models.Match.Attachments))]
        public Match? Match { get; set; }

        /// <summary>The reporter that uploaded it. In a peer-to-peer match each player may upload their own demo.</summary>
        public Guid? ReporterId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ReporterId))]
        [InverseProperty(nameof(MatchReporter.Attachments))]
        public MatchReporter? Reporter { get; set; }

        public MatchAttachmentKind Kind { get; set; }

        [MaxLength(255)]
        public string FileName { get; set; } = "";

        [MaxLength(255)]
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>The size in bytes the uploader declared, and once uploaded, the size received.</summary>
        public long Size { get; set; }

        /// <summary>Hex-encoded SHA-256 the uploader declared, if any. Checked when the upload completes.</summary>
        [MaxLength(64)]
        public string? Sha256 { get; set; }

        public MatchAttachmentStatus Status { get; set; } = MatchAttachmentStatus.Pending;

        /// <summary>Milliseconds of game time where the file starts, so tools can jump from an event to it.</summary>
        public long? StartMilliseconds { get; set; }

        /// <summary>Milliseconds of game time where the file ends.</summary>
        public long? EndMilliseconds { get; set; }

        public DateTime? UploadedOn { get; set; }

        /// <summary>When the file is deleted. Null keeps it.</summary>
        public DateTime? ExpiresOn { get; set; }

        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(Models.StorageLocation.MatchAttachments))]
        public StorageLocation? StorageLocation { get; set; }
    }
}
