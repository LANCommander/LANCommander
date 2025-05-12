using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Launcher.Data.Models;

[Table("Users")]
public class User : BaseModel
{
    public string? UserName { get; set; }
    public string? Alias { get; set; }
    public Media? Avatar { get; set; }
    public Library? Library { get; set; }

    [JsonIgnore]
    [NotMapped]
    public string? GetUserNameSafe => Alias ?? UserName;
}