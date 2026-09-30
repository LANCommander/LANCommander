using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A redistributable a game version needs, with that version's values for the redistributable's
    /// options (JSON of flattened option keys to values, as on the GameRedistributable join).
    /// </summary>
    [Table("GameVersionRedistributables")]
    public class GameVersionRedistributable
    {
        public Guid GameVersionId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameVersionId))]
        [InverseProperty(nameof(Models.GameVersion.Redistributables))]
        public GameVersion GameVersion { get; set; }

        public Guid RedistributableId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(RedistributableId))]
        public Redistributable Redistributable { get; set; }

        public string? Options { get; set; }
    }
}
