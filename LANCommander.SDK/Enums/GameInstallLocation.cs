using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    /// <summary>
    /// Where a game's archive is extracted. Main games always use <see cref="OwnDirectory"/>.
    /// </summary>
    public enum GameInstallLocation
    {
        /// <summary>
        /// {InstallRoot}\{DirectoryName or Title}
        /// </summary>
        [Display(Name = "Own Directory")]
        OwnDirectory = 0,

        /// <summary>
        /// Extracted directly over the base game's install directory
        /// </summary>
        [Display(Name = "Base Game's Directory")]
        BaseGameDirectory = 1,

        /// <summary>
        /// {BaseGameDirectory}\{DirectoryName or Title}
        /// </summary>
        [Display(Name = "Sub Directory")]
        SubDirectory = 2,
    }
}
