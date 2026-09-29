using System;
using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    /// <summary>
    /// Classifies a game. Install behavior and library visibility are driven by
    /// <see cref="GameInstallLocation"/> and the ShowInLibrary flag, not by this value.
    /// </summary>
    /// <remarks>
    /// Values are numbered explicitly because the C++ SDK casts the raw integer.
    /// </remarks>
    public enum GameType
    {
        [Display(Name = "Main Game")]
        MainGame = 0,
        Expansion = 1,
        [Obsolete("Use Expansion with InstallTo = OwnDirectory and ShowInLibrary = true")]
        [Display(Name = "Standalone Expansion")]
        StandaloneExpansion = 2,
        Mod = 3,
        [Obsolete("Use Mod with InstallTo = BaseGameDirectory and ShowInLibrary = true")]
        [Display(Name = "Standalone Mod")]
        StandaloneMod = 4
    }
}
