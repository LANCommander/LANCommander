using System;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// A game's server as players see it: where it is and whether it's up. Carries none of the
    /// server's paths, scripts or arguments, which are only for administrators.
    /// </summary>
    public class GameServer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public GameServerStatus Status { get; set; }

        /// <summary>The server isn't necessarily running, but starts when someone plays the game.</summary>
        public bool StartsOnPlayerActivity { get; set; }
    }

    public enum GameServerStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error,
    }
}
