using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LibraryAssets
{
    [JsonPropertyName("library_capsule")]
    public string? Capsule { get; set; }
    
    [JsonPropertyName("library_hero")]
    public string? Hero { get; set; }
    
    [JsonPropertyName("library_logo")]
    public string? Logo { get; set; }
    
    [JsonPropertyName("logo_position")]
    public LogoPosition? LogoPosition { get; set; }
}