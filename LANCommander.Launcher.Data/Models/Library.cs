using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models;

public class Library : BaseModel
{
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User User { get; set; }
    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}