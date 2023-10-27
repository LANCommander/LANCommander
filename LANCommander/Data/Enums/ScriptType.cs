using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Enums
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
        DetectInstall
    }
}
