using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models;

public class GameCustomField : BaseModel
{
    [MaxLength(64)]
    public string Name { get; set; }

    [MaxLength(1024)]
    public string Value { get; set; }

    public Guid? GameId { get; set; }
    [JsonIgnore]
    [ForeignKey(nameof(GameId))]
    [InverseProperty("CustomFields")]
    public Game? Game { get; set; }
}