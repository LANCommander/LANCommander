using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models;

public class ServerPort : BaseModel
{
    public int Number { get; set; }
    public string Name { get; set; }
    public Guid? ServerId { get; set; }
    [JsonIgnore]
    [ForeignKey(nameof(ServerId))]
    [InverseProperty("Ports")]
    public Server? Server { get; set; }
}