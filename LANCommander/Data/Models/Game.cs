using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public long? IGDBId { get; set; }
        public string Title { get; set; }
        [Display(Name = "Sort Title")]
        public string? SortTitle { get; set; }
        public string? Icon { get; set; }
        [Display(Name = "Directory Name")]
        public string? DirectoryName { get; set; }
        public string? Description { get; set; }
        [Display(Name = "Released On")]
        public DateTime? ReleasedOn { get; set; }

        public virtual ICollection<Action>? Actions { get; set; }

        public bool Singleplayer { get; set; } = false;

        public virtual ICollection<MultiplayerMode>? MultiplayerModes { get; set; }
        public virtual ICollection<Genre>? Genres { get; set; }
        public virtual ICollection<Tag>? Tags { get; set; }
        public virtual ICollection<Category>? Categories { get; set; }
        public virtual ICollection<Company>? Publishers { get; set; }
        public virtual ICollection<Company>? Developers { get; set; }
        public virtual ICollection<Archive>? Archives { get; set; }
        public virtual ICollection<Script>? Scripts { get; set; }

        public string? ValidKeyRegex { get; set; }
        public virtual ICollection<Key>? Keys { get; set; }
    }
}
