using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Engines")]
    public class Engine : BaseModel
    {
        public string Name { get; set; }
        public virtual ICollection<Game> Games { get; set; }
    }
}
