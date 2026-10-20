using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Games")]
    public class Game : BaseModel
    {
        public virtual ICollection<GameExternalId>? ExternalIds { get; set; } = new List<GameExternalId>();
        public string Title { get; set; }
        [Display(Name = "Sort Title")]
        public string? SortTitle { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }

        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
        public string? LatestVersion { get; set; }

        /// <summary>The server's id for the installed version, when the install was made against a known version.</summary>
        public Guid? InstalledVersionId { get; set; }

        /// <summary>
        /// Queue updates as soon as they're found. Turned off to keep the game on its current version,
        /// and turned off automatically when the game is rolled back.
        /// </summary>
        public bool AutoUpdate { get; set; } = true;

        /// <summary>
        /// Whether the library knows of a newer version than the one installed and the game takes updates.
        /// A quick local check; the server's CheckForUpdate is authoritative.
        /// </summary>
        public bool IsUpdateAvailable()
            => Installed
                && AutoUpdate
                && !string.IsNullOrWhiteSpace(LatestVersion)
                && InstalledVersion != LatestVersion;

        [Display(Name = "Released On")]
        public DateTime? ReleasedOn { get; set; }

        public GameType Type { get; set; }
        public GameInstallLocation InstallTo { get; set; }
        public bool ShowInLibrary { get; set; } = true;

        public string? OptionSchema { get; set; }
        public string? Options { get; set; }

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
        public virtual ICollection<Tool>? Tools { get; set; } = new List<Tool>();
        public virtual ICollection<GameTool> GameTools { get; set; } = new List<GameTool>();
        public virtual ICollection<Media>? Media { get; set; } = new List<Media>();
        public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public virtual ICollection<Game> DependentGames { get; set; } = new List<Game>();
        public virtual ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
        public virtual ICollection<Library> Libraries { get; set; } = new List<Library>();
    }
}
