using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Genres")]
    public class Genre : BaseModel
    {
        public string Name { get; set; }
        public virtual ICollection<Game> Games { get; set; }
    }
}
