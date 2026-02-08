using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LogoPosition
{
    [JsonPropertyName("height_pct")]
    public string? HeightPercentage { get; set; }
    
    [JsonPropertyName("width_pct")]
    public string? WidthPercentage { get; set; }
    
    [JsonPropertyName("pinned_position")]
    public string? PinnedPosition { get; set; }
}