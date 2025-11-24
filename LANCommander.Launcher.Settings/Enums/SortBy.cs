using System.ComponentModel.DataAnnotations;

namespace LANCommander.Launcher.Settings.Enums
{
    public enum SortBy
    {
        Title,
        [Display(Name = "Date Added")]
        DateAdded,
        [Display(Name = "Date Released")]
        DateReleased,
        [Display(Name = "Recent Activity")]
        RecentActivity,
        [Display(Name = "Most Played")]
        MostPlayed
    }
}
