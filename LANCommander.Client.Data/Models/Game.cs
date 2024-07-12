using LANCommander.Client.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Client.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public long? IGDBId { get; set; }
        public string Title { get; set; }
        [Display(Name = "Sort Title")]
        public string? SortTitle { get; set; }
        [Display(Name = "Directory Name")]
        public string? Description { get; set; }
        public string? Notes { get; set; }

        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public string? LatestVersion { get; set; }

        [Display(Name = "Released On")]
        public DateTime? ReleasedOn { get; set; }

        public GameType Type { get; set; }
        public Guid? BaseGameId { get; set; }
        [ForeignKey(nameof(BaseGameId))]
        public virtual Game? BaseGame { get; set; }

        public bool Singleplayer { get; set; } = false;

        public Guid? EngineId { get; set; }
        [ForeignKey(nameof(EngineId))]
        public virtual Engine Engine { get; set; }

        public virtual ICollection<MultiplayerMode>? MultiplayerModes { get; set; } = new List<MultiplayerMode>();
        public virtual ICollection<Genre>? Genres { get; set; } = new List<Genre>();
        public virtual ICollection<Tag>? Tags { get; set; } = new List<Tag>();
        public virtual ICollection<Category>? Categories { get; set; } = new List<Category>();
        public virtual ICollection<Company>? Publishers { get; set; } = new List<Company>();
        public virtual ICollection<Company>? Developers { get; set; } = new List<Company>();
        public virtual ICollection<Platform>? Platforms { get; set; } = new List<Platform>();
        public virtual ICollection<Redistributable>? Redistributables { get; set; } = new List<Redistributable>();
        public virtual ICollection<Media>? Media { get; set; } = new List<Media>();
        public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public virtual ICollection<Game> DependentGames { get; set; } = new List<Game>();
        public virtual ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
    }
}
