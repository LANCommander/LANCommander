using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models;

[Table("Ratings")]
public class Rating : BaseModel
{
    public Guid? GameId { get; set; }
    [JsonIgnore]
    [ForeignKey(nameof(GameId))]
    [InverseProperty("Ratings")]
    public virtual Game? Game { get; set; }
    
    [MaxLength(128)]
    public string? Source { get; set; }
    
    public float Value { get; set; }
}