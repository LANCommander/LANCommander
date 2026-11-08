using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>What a file attached to a match holds.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchAttachmentKind
    {
        Demo,
        Replay,
        Log,
        Screenshot,
        Other,
    }
}
