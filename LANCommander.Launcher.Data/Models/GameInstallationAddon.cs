using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    /// <summary>
    /// Join entity between a <see cref="GameInstallation"/> and an add-on <see cref="Game"/>
    /// (a <see cref="Game"/> row of type <c>Expansion</c>/<c>Mod</c> whose <c>BaseGameId</c> points
    /// back to the base game). Add-ons currently track their own install state globally via the
    /// legacy <see cref="Game.Installed"/>/<see cref="Game.InstallDirectory"/> fields, which cannot
    /// distinguish which side-by-side base installation they were installed into. This association
    /// scopes an add-on's install state to a specific base installation directory instead.
    /// </summary>
    [Table("GameInstallationAddons")]
    public class GameInstallationAddon
    {
        public Guid GameInstallationId { get; set; }
        public virtual GameInstallation GameInstallation { get; set; }

        public Guid AddonGameId { get; set; }
        [ForeignKey(nameof(AddonGameId))]
        public virtual Game AddonGame { get; set; }

        /// <summary>
        /// The server-side full archive of the add-on that was installed. The first release only
        /// exposes base-game archive selection, so this is nullable/unused for now, but is present
        /// so per-installation add-on version selection can be added later without another schema
        /// change.
        /// </summary>
        public Guid? ArchiveId { get; set; }

        public bool Installed { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
    }
}
