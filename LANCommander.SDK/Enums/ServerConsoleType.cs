using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    public enum ServerConsoleType
    {
        [Display(Name = "Log File")]
        LogFile,
        RCON
    }
}
