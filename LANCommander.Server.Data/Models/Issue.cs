using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Issues")]
    public class Issue : BaseModel
    {
        [MaxLength(128)]
        public string? Title { get; set; }

        public string Description { get; set; }

        /// <summary>Pinned issues are listed first wherever a game's issues are shown.</summary>
        [Display(Name = "Pinned")]
        public bool IsPinned { get; set; }
        public DateTime? ResolvedOn { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Issues")]
        public Game Game { get; set; }

        [Display(Name = "Resolved By")]
        public User? ResolvedBy { get; set; }

        public ICollection<IssueAttachment>? Attachments { get; set; }
    }
}
