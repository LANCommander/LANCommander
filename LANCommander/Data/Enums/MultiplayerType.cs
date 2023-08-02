using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Enums
{
    public enum MultiplayerType
    {
        Local,
        [Display(Name = "LAN")]
        Lan,
        Online
    }
}
