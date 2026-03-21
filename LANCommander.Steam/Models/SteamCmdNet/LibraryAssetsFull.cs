using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LibraryAssetsFull
{
    [JsonPropertyName("library_capsule")]
    public LibraryAssetVariant? Capsule { get; set; }
    
    [JsonPropertyName("library_hero")]
    public LibraryAssetVariant? Hero { get; set; }
    
    [JsonPropertyName("library_logo")]
    public LibraryAssetVariant? Logo { get; set; }
}