using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class SupportedLanguage
{
    [JsonPropertyName("supported")]
    public string? Supported { get; set; }
    
    [JsonPropertyName("full_audio")]
    public string? FullAudio { get; set; }
}