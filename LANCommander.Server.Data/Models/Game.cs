using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public ICollection<GameExternalId>? ExternalIds { get; set; }
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

        /// <summary>
        /// Where the game's archive is extracted. Always <see cref="GameInstallLocation.OwnDirectory"/>
        /// for main games. <see cref="DirectoryName"/> overrides the folder name for own and sub directories.
        /// </summary>
        [Display(Name = "Install To")]
        public GameInstallLocation InstallTo { get; set; } = GameInstallLocation.OwnDirectory;

        /// <summary>
        /// Whether the game appears as its own entry in the depot and library. Always true for main
        /// games. Addons that aren't shown are offered as options when installing their base game.
        /// </summary>
        [Display(Name = "Show in Library")]
        public bool ShowInLibrary { get; set; } = true;

        public Guid? BaseGameId { get; set; }
        [ForeignKey(nameof(BaseGameId))]
        public virtual Game? BaseGame { get; set; }

        public bool Singleplayer { get; set; } = false;

        /// <summary>
        /// Whether the game is visible to launcher users (depot, library). Hidden games are
        /// still fully manageable by administrators.
        /// </summary>
        public bool Published { get; set; } = true;

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
        public ICollection<GameVersion>? Versions { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<GameSave>? GameSaves { get; set; }
        public ICollection<PlaySession>? PlaySessions { get; set; }
        public ICollection<SavePath>? SavePaths { get; set; }
        public ICollection<Server>? Servers { get; set; }
        public ICollection<Redistributable>? Redistributables { get; set; }
        public ICollection<Tool>? Tools { get; set; }
        public ICollection<Media>? Media { get; set; }

        public string? OptionSchema { get; set; }

        public string? ValidKeyRegex { get; set; }
        public ICollection<Key>? Keys { get; set; }
        public ICollection<Collection> Collections { get; set; }
        public ICollection<Game> DependentGames { get; set; }
        public ICollection<Issue> Issues { get; set; }
        public ICollection<Page>? Pages { get; set; }
        public ICollection<Library> Libraries { get; set; }
        public ICollection<Rating>? Ratings { get; set; }
        public ICollection<GameCustomField>? CustomFields { get; set; }
        
        [NotMapped]
        public bool IsAddon => Type != GameType.MainGame;

        /// <summary>The letter a title is filed under in an A–Z list; "#" for digits and symbols.</summary>
        [NotMapped]
        public string TitleInitial => InitialOf(Title);

        public static string InitialOf(string? title) =>
            !string.IsNullOrEmpty(title) && char.IsLetter(title[0]) ? char.ToUpperInvariant(title[0]).ToString() : "#";

        /// <summary>
        /// Dependent games that aren't shown in the library on their own, and are instead
        /// selected when installing this game
        /// </summary>
        [NotMapped]
        public IEnumerable<Game> Addons {
            get
            {
                if (DependentGames != null)
                    return DependentGames.Where(g => g.IsAddon && !g.ShowInLibrary);
                else
                    return [];
            }
        }
    }
}
