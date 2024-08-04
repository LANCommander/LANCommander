using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.Data.Enums
{
    public enum ServerConsoleType
    {
        [Display(Name = "Log File")]
        LogFile,
        RCON
    }
}
