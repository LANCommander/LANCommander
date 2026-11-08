using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>How a match participant was linked to a LANCommander user.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchParticipantLinkSource
    {
        /// <summary>The participant isn't linked to a user.</summary>
        None,

        /// <summary>A trusted reporter named the user.</summary>
        Reported,

        /// <summary>A known player identity, such as a Steam ID, matched.</summary>
        Identity,

        /// <summary>The user was playing the game on the match's server under the participant's name.</summary>
        Session,

        /// <summary>An administrator linked the participant by hand.</summary>
        Manual,
    }
}
