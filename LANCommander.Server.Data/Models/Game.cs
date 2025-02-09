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

        public virtual ICollection<Action>? Actions { get; set; }

        public KeyAllocationMethod KeyAllocationMethod { get; set; } = KeyAllocationMethod.UserAccount;

        public GameType Type { get; set; }
        public Guid? BaseGameId { get; set; }
        [ForeignKey(nameof(BaseGameId))]
        public virtual Game? BaseGame { get; set; }

        public bool Singleplayer { get; set; } = false;

        public Guid? EngineId { get; set; }
        [ForeignKey(nameof(EngineId))]
        public virtual Engine Engine { get; set; }

        public ICollection<MultiplayerMode>? MultiplayerModes { get; set; }
        public ICollection<Genre>? Genres { get; set; } = new List<Genre>();
        public ICollection<Tag>? Tags { get; set; } = new List<Tag>();
        public ICollection<Platform>? Platforms { get; set; }
        public ICollection<Category>? Categories { get; set; }
        public ICollection<Company>? Publishers { get; set; }
        public ICollection<Company>? Developers { get; set; }
        public ICollection<Archive>? Archives { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<GameSave>? GameSaves { get; set; }
        public ICollection<PlaySession>? PlaySessions { get; set; }
        public ICollection<SavePath>? SavePaths { get; set; }
        public ICollection<Server>? Servers { get; set; }
        public ICollection<Redistributable>? Redistributables { get; set; }
        public ICollection<Media>? Media { get; set; }

        public string? ValidKeyRegex { get; set; }
        public ICollection<Key>? Keys { get; set; }
        public ICollection<Collection> Collections { get; set; }
        public ICollection<Game> DependentGames { get; set; }
        public ICollection<Issue> Issues { get; set; }
        public ICollection<Page>? Pages { get; set; }
        public ICollection<Library> Libraries { get; set; }
        public ICollection<GameCustomField>? CustomFields { get; set; }
    }
}
