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

        public KeyAllocationMethod KeyAllocationMethod { get; set; } = KeyAllocationMethod.UserAccount;

        public GameType Type { get; set; }
        public Guid? BaseGameId { get; set; }
        [ForeignKey(nameof(BaseGameId))]
        public virtual Game? BaseGame { get; set; }

        public bool Singleplayer { get; set; } = false;

        public Guid? EngineId { get; set; }
        [ForeignKey(nameof(EngineId))]
        public virtual Engine Engine { get; set; }

        public ICollection<MultiplayerMode>? MultiplayerModes { get; set; } = new List<MultiplayerMode>();
        public ICollection<Genre>? Genres { get; set; } = new List<Genre>();
        public ICollection<Tag>? Tags { get; set; } = new List<Tag>();
        public ICollection<Platform>? Platforms { get; set; } = new List<Platform>();
        public ICollection<Category>? Categories { get; set; } = new List<Category>();
        public ICollection<Company>? Publishers { get; set; } = new List<Company>();
        public ICollection<Company>? Developers { get; set; } = new List<Company>();
        public ICollection<Archive>? Archives { get; set; } = new List<Archive>();
        public ICollection<Script>? Scripts { get; set; } = new List<Script>();
        public ICollection<GameSave>? GameSaves { get; set; } = new List<GameSave>();
        public ICollection<PlaySession>? PlaySessions { get; set; } = new List<PlaySession>();
        public ICollection<SavePath>? SavePaths { get; set; } = new List<SavePath>();
        public ICollection<Server>? Servers { get; set; } = new List<Server>();
        public ICollection<Redistributable>? Redistributables { get; set; } = new List<Redistributable>();
        public ICollection<Media>? Media { get; set; } = new List<Media>();

        public string? ValidKeyRegex { get; set; }
        public ICollection<Key>? Keys { get; set; } = new List<Key>();
        public ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public ICollection<Game> DependentGames { get; set; } = new List<Game>();
        public ICollection<Issue> Issues { get; set; } = new List<Issue>();
        public ICollection<Page>? Pages { get; set; }
        public ICollection<Library> Libraries { get; set; } = new List<Library>();
    }
}
