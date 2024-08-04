using System.ComponentModel.DataAnnotations;

namespace LANCommander.Launcher.Enums
{
    public enum DownloadStatus
    {
        Idle,
        Downloading,
        [Display(Name = "Installing Redistributables")]
        InstallingRedistributables,
        [Display(Name = "Installing Mods")]
        InstallingMods,
        [Display(Name = "Installing Expansions")]
        InstallingExpansions,
        [Display(Name = "Running Scripts")]
        RunningScripts,
        [Display(Name = "Downloading Saves")]
        DownloadingSaves,
        Canceled,
        Failed,
        Complete
    }
}
