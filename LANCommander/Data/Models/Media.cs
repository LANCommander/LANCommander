using LANCommander.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    [Table("Media")]
    public class Media : BaseModel
    {
        public Guid FileId { get; set; }
        public MediaType Type { get; set; }

        [MaxLength(2048)]
        public string SourceUrl { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Media")]
        public virtual Game? Game { get; set; }
    }
}
