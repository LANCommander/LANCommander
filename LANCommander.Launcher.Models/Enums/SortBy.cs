using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models.Enums
{
    public enum SortBy
    {
        Title,
        [Display(Name = "Date Added")]
        DateAdded,
        [Display(Name = "Date Released")]
        DateReleased,
        [Display(Name = "Recent Activity")]
        RecentActivity
    }
}
