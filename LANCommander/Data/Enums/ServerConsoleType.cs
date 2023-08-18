using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Enums
{
    public enum ServerConsoleType
    {
        [Display(Name = "Log File")]
        LogFile,
        RCON
    }
}
