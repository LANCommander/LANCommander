using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public long? IGDBId { get; set; }
        public string Title { get; set; }
        [Display(Name = "Sort Title")]
        public string? SortTitle { get; set; }
        [Display(Name = "Directory Name")]
        public string? DirectoryName { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }

        [Display(Name = "Released On")]
        public DateTime? ReleasedOn { get; set; }

        public virtual ICollection<Action>? Actions { get; set; } = new List<Action>();

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
        public virtual ICollection<Platform>? Platforms { get; set; } = new List<Platform>();
        public virtual ICollection<Category>? Categories { get; set; } = new List<Category>();
        public virtual ICollection<Company>? Publishers { get; set; } = new List<Company>();
        public virtual ICollection<Company>? Developers { get; set; } = new List<Company>();
        public virtual ICollection<Archive>? Archives { get; set; } = new List<Archive>();
        public virtual ICollection<Script>? Scripts { get; set; } = new List<Script>();
        public virtual ICollection<GameSave>? GameSaves { get; set; } = new List<GameSave>();
        public virtual ICollection<PlaySession>? PlaySessions { get; set; } = new List<PlaySession>();
        public virtual ICollection<SavePath>? SavePaths { get; set; } = new List<SavePath>();
        public virtual ICollection<Server>? Servers { get; set; } = new List<Server>();
        public virtual ICollection<Redistributable>? Redistributables { get; set; } = new List<Redistributable>();
        public virtual ICollection<Media>? Media { get; set; } = new List<Media>();

        public string? ValidKeyRegex { get; set; }
        public virtual ICollection<Key>? Keys { get; set; } = new List<Key>();
        public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public virtual ICollection<Game> DependentGames { get; set; } = new List<Game>();
        public virtual ICollection<Issue> Issues { get; set; } = new List<Issue>();
    }
}
