using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    /// <summary>
    /// Join entity between <see cref="GameInstallation"/> and <see cref="Tool"/> that tracks the
    /// install state of a tool for a specific installation instance. Because a game can now have
    /// multiple side-by-side installations, tool install state can no longer be tracked per game
    /// alone (see the legacy <see cref="GameTool"/>, kept for transitional compatibility) — it must
    /// be scoped to the exact installation directory the tool was installed into.
    /// </summary>
    [Table("GameInstallationTools")]
    public class GameInstallationTool
    {
        public Guid GameInstallationId { get; set; }
        public virtual GameInstallation GameInstallation { get; set; }

        public Guid ToolId { get; set; }
        public virtual Tool Tool { get; set; }

        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
    }
}
