using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public string Title { get; set; }
        public string? SortTitle { get; set; }
        public string? DirectoryName { get; set; }
        public string Description { get; set; }
        public DateTime ReleasedOn { get; set; }

        public virtual ICollection<Tag>? Tags { get; set; }

        public virtual Company? Publisher { get; set; }
        public virtual Company? Developer { get; set; }

        public virtual ICollection<Archive>? Archives { get; set; }
    }
}
