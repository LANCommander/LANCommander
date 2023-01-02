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

        public ICollection<Tag>? Tags { get; set; }

        public Company? Publisher { get; set; }
        public Company? Developer { get; set; }

        public ICollection<Archive>? Archives { get; set; }
    }
}
