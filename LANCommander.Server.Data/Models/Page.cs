using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Pages")]
    public class Page : BaseModel
    {
        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(256)]
        public string Slug { get; set; }

        [MaxLength(2048)]
        public string Route { get; set; }
        public string Contents { get; set; }

        public int SortOrder { get; set; }

        public Guid? ParentId { get; set; }
        [ForeignKey(nameof(ParentId))]
        public Page? Parent { get; set; }

        public ICollection<Page> Children { get; set; }

        public ICollection<Game> Games { get; set; }
        public ICollection<Redistributable> Redistributables { get; set; }
        public ICollection<Server> Servers { get; set; }
    }
}
