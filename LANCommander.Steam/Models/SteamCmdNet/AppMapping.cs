using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppMapping
{
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("tool")]
    public string? Tool { get; set; }
}