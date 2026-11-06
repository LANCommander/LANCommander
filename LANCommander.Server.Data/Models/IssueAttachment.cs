using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A file a user attached to an issue, such as a screenshot or a log. The file is stored under
    /// its storage location by id; <see cref="FileName"/> is only what it was called when uploaded.
    /// </summary>
    [Table("IssueAttachments")]
    public class IssueAttachment : BaseModel
    {
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public long Size { get; set; }

        public Guid IssueId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(IssueId))]
        [InverseProperty(nameof(Models.Issue.Attachments))]
        public Issue? Issue { get; set; }

        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(Models.StorageLocation.IssueAttachments))]
        public StorageLocation? StorageLocation { get; set; }
    }
}
