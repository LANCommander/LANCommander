using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    public enum ServerAutostartMethod
    {
        [Display(Name = "On Application Start")]
        OnApplicationStart,
        [Display(Name = "On Player Activity")]
        OnPlayerActivity
    }
}
