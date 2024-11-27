using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Issues")]
    public class Issue : BaseModel
    {
        public string Description { get; set; }
        public DateTime? ResolvedOn { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Issues")]
        public Game Game { get; set; }

        [Display(Name = "Resolved By")]
        public User? ResolvedBy { get; set; }
    }
}
