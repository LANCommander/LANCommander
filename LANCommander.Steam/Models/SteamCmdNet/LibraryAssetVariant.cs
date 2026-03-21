using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LibraryAssetVariant
{
    [JsonPropertyName("image")]
    public Dictionary<string, string>? Image { get; set; }
    
    [JsonPropertyName("image2x")]
    public Dictionary<string, string>? ImageLarge { get; set; }
    
    [JsonPropertyName("logo_position")]
    public LogoPosition? Position { get; set; }
}