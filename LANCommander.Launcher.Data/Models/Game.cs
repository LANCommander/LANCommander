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
        [Display(Name = "Directory Name")]
        public string? Description { get; set; }
        public string? Notes { get; set; }

        // Legacy single-install fields. Retained for transitional compatibility while
        // GameInstallation-based side-by-side installs are phased in (see Installations below);
        // do not remove until callers are migrated to installation instances in a later phase.
        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
        public string? LatestVersion { get; set; }

        /// <summary>
        /// All local installation instances of this game. A game may have zero (not installed),
        /// one, or several side-by-side installations, each pinned to its own directory and
        /// (optionally) server archive.
        /// </summary>
        public virtual ICollection<GameInstallation> Installations { get; set; } = new List<GameInstallation>();

        /// <summary>
        /// Convenience pointer to the currently active/selected installation, kept in sync with
        /// the corresponding <see cref="GameInstallation.IsSelected"/> flag by
        /// <c>GameInstallationService</c>. Prefer <see cref="SelectedInstallation"/> or
        /// <see cref="CurrentInstallation"/> for reads; this FK exists mainly so the selected
        /// installation can be resolved without loading the whole <see cref="Installations"/>
        /// collection.
        /// </summary>
        public Guid? SelectedInstallationId { get; set; }
        [ForeignKey(nameof(SelectedInstallationId))]
        public virtual GameInstallation? SelectedInstallation { get; set; }

        /// <summary>
        /// True when this game has at least one local installation instance. Prefer this over the
        /// legacy <see cref="Installed"/> flag once callers are migrated to installation instances.
        /// </summary>
        [NotMapped]
        public bool HasInstallations => Installations != null && Installations.Count > 0;

        /// <summary>
        /// The installation instance currently marked as selected/active, derived from the loaded
        /// <see cref="Installations"/> collection. Falls back to <see cref="SelectedInstallation"/>
        /// when <see cref="Installations"/> has not been loaded, and finally to the single legacy
        /// installation shape (so existing single-install callers keep working) by synthesizing a
        /// transient instance from the legacy fields when no installation instances exist yet.
        /// </summary>
        [NotMapped]
        public GameInstallation? CurrentInstallation
        {
            get
            {
                if (Installations != null && Installations.Count > 0)
                    return Installations.FirstOrDefault(i => i.IsSelected) ?? Installations.First();

                if (SelectedInstallation != null)
                    return SelectedInstallation;

                if (Installed && !string.IsNullOrEmpty(InstallDirectory))
                {
                    return new GameInstallation
                    {
                        GameId = Id,
                        Game = this,
                        Version = InstalledVersion,
                        InstallDirectory = InstallDirectory,
                        InstalledOn = InstalledOn,
                        IsSelected = true,
                    };
                }

                return null;
            }
        }

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
        public virtual ICollection<Tool>? Tools { get; set; } = new List<Tool>();
        public virtual ICollection<GameTool> GameTools { get; set; } = new List<GameTool>();
        public virtual ICollection<Media>? Media { get; set; } = new List<Media>();
        public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public virtual ICollection<Game> DependentGames { get; set; } = new List<Game>();
        public virtual ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
        public virtual ICollection<Library> Libraries { get; set; } = new List<Library>();
    }
}
