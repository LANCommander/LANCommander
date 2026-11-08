using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>How a player or team finished a match.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchResultStatus
    {
        Finished,
        DidNotFinish,
        Disqualified,
        Defeated,
        Forfeited,
    }
}
