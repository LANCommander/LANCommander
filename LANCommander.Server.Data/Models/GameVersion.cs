using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("GameVersions")]
    public class GameVersion : BaseModel
    {
        [Required]
        public string Version { get; set; }

        public string? Changelog { get; set; }

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty(nameof(Models.Game.Versions))]
        public Game Game { get; set; }

        public Archive? Archive { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<Action>? Actions { get; set; }
        public ICollection<SavePath>? SavePaths { get; set; }
    }
}
