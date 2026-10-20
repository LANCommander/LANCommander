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

        /// <summary>
        /// Whether launchers are offered this version. New versions are drafts until published; creating
        /// a newer version publishes the current one.
        /// </summary>
        public bool Published { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty(nameof(Models.Game.Versions))]
        public Game Game { get; set; }

        public Archive? Archive { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<Action>? Actions { get; set; }
        public ICollection<SavePath>? SavePaths { get; set; }

        /// <summary>The game's option schema (YAML) as of this version.</summary>
        [Display(Name = "Option Schema")]
        public string? OptionSchema { get; set; }

        /// <summary>The redistributables this version needs, with its option values for each.</summary>
        public ICollection<GameVersionRedistributable>? Redistributables { get; set; }
    }
}
