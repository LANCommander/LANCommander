using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models;

[Table("Users")]
public class User : BaseModel
{
    public string? UserName { get; set; }
    public string? Alias { get; set; }
    public Media? Avatar { get; set; }
    public Library? Library { get; set; }
}