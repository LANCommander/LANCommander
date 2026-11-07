namespace LANCommander.SDK.Enums
{
    /// <summary>
    /// What an action is for, which decides where a launcher offers it. Replaces the old primary flag:
    /// an action that was primary is <see cref="Primary"/>, any other is <see cref="Secondary"/>.
    /// </summary>
    /// <remarks>
    /// The values are stored and sent as numbers. <see cref="Secondary"/> is zero so an action that
    /// doesn't say what it is never becomes the one the Play button runs.
    /// </remarks>
    public enum ActionType
    {
        /// <summary>Plays the game: the Play button and the default for links and the tray.</summary>
        Primary = 1,

        /// <summary>Another way to play, offered in the Play button's menu.</summary>
        Secondary = 0,

        /// <summary>Configures the game (a setup tool, a settings program); never run by default.</summary>
        Config = 2,

        /// <summary>
        /// Joins a multiplayer game. Its arguments are a template the context fills in (a game server's
        /// host and port, a link's parameters), so a launcher doesn't list it alongside the play actions.
        /// </summary>
        Join = 3,
    }
}
