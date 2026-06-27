using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    /// <summary>
    /// Join entity between <see cref="Game"/> and <see cref="Tool"/> that tracks the install
    /// state of a tool for a specific game. Because a tool can be shared by multiple games and is
    /// installed into each game's own directory, the install state must be tracked per game rather
    /// than on the tool itself.
    /// </summary>
    [Table("GameTool")]
    public class GameTool
    {
        public Guid GameId { get; set; }
        public virtual Game Game { get; set; }

        public Guid ToolId { get; set; }
        public virtual Tool Tool { get; set; }

        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
    }
}
