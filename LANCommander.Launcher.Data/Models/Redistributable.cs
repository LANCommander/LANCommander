using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Redistributables")]
    public class Redistributable : BaseModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public virtual ICollection<Game>? Games { get; set; } = new List<Game>();
    }
}
