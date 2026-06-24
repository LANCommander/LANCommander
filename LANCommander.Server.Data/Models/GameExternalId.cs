using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models;

[Table("GameExternalIds")]
public class GameExternalId : BaseModel
{
    public Guid? GameId { get; set; }
    [JsonIgnore]
    [ForeignKey(nameof(GameId))]
    [InverseProperty("ExternalIds")]
    public virtual Game? Game { get; set; }

    [MaxLength(64)]
    public string Provider { get; set; }

    [MaxLength(256)]
    public string ExternalId { get; set; }
}
