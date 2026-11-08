using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>Where a multiplayer match is in its lifecycle.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchStatus
    {
        /// <summary>The match is being played and events are arriving.</summary>
        Open,

        /// <summary>A reporter ended the match. Late events are still accepted.</summary>
        Ended,

        /// <summary>The match stopped reporting without being ended, or was ended as aborted.</summary>
        Abandoned,
    }
}
