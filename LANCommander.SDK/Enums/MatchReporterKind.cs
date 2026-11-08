using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>What kind of machine or component sent a match's events.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchReporterKind
    {
        /// <summary>A dedicated game server, or something watching one.</summary>
        Server,

        /// <summary>The player hosting a peer-to-peer or listen-server match.</summary>
        Host,

        /// <summary>A player's own game or launcher.</summary>
        Client,

        /// <summary>A plugin running inside the LANCommander server.</summary>
        Plugin,
    }
}
