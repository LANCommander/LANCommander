using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    /// <summary>
    /// A single local installation instance of a <see cref="Game"/>. A game can have multiple
    /// installations side-by-side (for example two different pinned versions), each with its own
    /// install directory. Exactly one installation per game may be <see cref="IsSelected"/> at a
    /// time; this invariant is enforced transactionally by
    /// <see cref="Data.Models"/>-adjacent service logic (see
    /// <c>GameInstallationService</c>) rather than solely by the database, though a filtered
    /// unique index provides a safety net in SQLite.
    /// </summary>
    [Table("GameInstallations")]
    public class GameInstallation : BaseModel
    {
        public Guid GameId { get; set; }
        [ForeignKey(nameof(GameId))]
        public virtual Game Game { get; set; }

        /// <summary>
        /// The server-side full archive this installation was installed from. Nullable because
        /// historical installs (migrated from the legacy single-install <see cref="Game"/> fields)
        /// and servers that predate archive selection may not know which archive was used.
        /// </summary>
        public Guid? ArchiveId { get; set; }

        public string? Version { get; set; }

        [Required]
        public string InstallDirectory { get; set; } = string.Empty;

        public DateTime? InstalledOn { get; set; }

        /// <summary>
        /// Optional user/UI-facing label to distinguish installations of the same game
        /// (e.g. "1.2.0 (Beta)" or "D:\\Games\\MyGame - Old Version").
        /// </summary>
        public string? DisplayLabel { get; set; }

        /// <summary>
        /// Whether this is the active/selected installation used by default for play, modify,
        /// move, update, and uninstall actions. At most one installation per game may have this
        /// set to true.
        /// </summary>
        public bool IsSelected { get; set; }

        public virtual ICollection<GameInstallationTool> InstallationTools { get; set; } = new List<GameInstallationTool>();
        public virtual ICollection<GameInstallationAddon> InstallationAddons { get; set; } = new List<GameInstallationAddon>();
    }
}
