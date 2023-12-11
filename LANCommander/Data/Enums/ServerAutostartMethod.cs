using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Enums
{
    public enum ServerAutostartMethod
    {
        [Display(Name = "Application Start")]
        OnApplicationStart,
        [Display(Name = "Player Activity")]
        OnPlayerActivity
    }
}
