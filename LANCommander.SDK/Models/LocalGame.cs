using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// A game as the client running the script knows it: its local library entry and install state.
    /// Returned by <see cref="Abstractions.ILocalLibraryProvider"/> and the <c>Get-LocalGame</c> cmdlet.
    /// </summary>
    public class LocalGame
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public GameType Type { get; set; }

        /// <summary>The game this one extends, for addons, expansions and mods.</summary>
        public Guid? BaseGameId { get; set; }

        public bool Installed { get; set; }
        public string InstallDirectory { get; set; }
        public string InstalledVersion { get; set; }
        public Guid? InstalledVersionId { get; set; }
        public DateTime? InstalledOn { get; set; }
        public string LatestVersion { get; set; }
    }
}
