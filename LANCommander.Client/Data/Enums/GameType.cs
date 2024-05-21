using System.ComponentModel.DataAnnotations;

namespace LANCommander.Client.Data.Enums
{
    public enum GameType
    {
        [Display(Name = "Main Game")]
        MainGame,
        Expansion,
        [Display(Name = "Standalone Expansion")]
        StandaloneExpansion,
        Mod,
        [Display(Name = "Standalone Mod")]
        StandaloneMod
    }
}
