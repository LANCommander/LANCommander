using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class ExternalArguments
{
    [JsonPropertyName("allowunknown")]
    public string? AllowUnknown { get; set; }
}