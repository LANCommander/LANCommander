using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    public enum ScriptType
    {
        Install,
        Uninstall,
        [Display(Name = "Name Change")]
        NameChange,
        [Display(Name = "Key Change")]
        KeyChange,
        [Display(Name = "Save Upload")]
        SaveUpload,
        [Display(Name = "Save Download")]
        SaveDownload,
        [Display(Name = "Detect Install")]
        DetectInstall,
        [Display(Name = "Before Start")]
        BeforeStart,
        [Display(Name = "After Stop")]
        AfterStop,
        [Display(Name = "Game Started")]
        GameStarted,
        [Display(Name = "Game Ended")]
        GameStopped,
        [Display(Name = "User Registration")]
        UserRegistration,
        [Display(Name = "User Login")]
        UserLogin,
        [Display(Name = "Application Start")]
        ApplicationStart,
    }
}
