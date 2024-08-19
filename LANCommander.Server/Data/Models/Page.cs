using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Pages")]
    public class Page : BaseModel
    {
        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(2048)]
        public string Route { get; set; }
        public string Contents { get; set; }

        public Guid? ParentId { get; set; }
        [ForeignKey(nameof(ParentId))]
        public virtual Page? Parent { get; set; }

        public virtual ICollection<Page> Children { get; set; }

        public virtual ICollection<Game> Games { get; set; }
        public virtual ICollection<Redistributable> Redistributables { get; set; }
        public virtual ICollection<Server> Servers { get; set; }
    }
}
