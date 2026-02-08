using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppInfo
{
    [JsonPropertyName("_change_number")]
    public long? ChangeNumber { get; set; }
    
    [JsonPropertyName("_missing_token")]
    public bool? MissingToken { get; set; }
    
    [JsonPropertyName("_sha")]
    public string? SHA { get; set; }
    
    [JsonPropertyName("_size")]
    public long? Size { get; set; }
    
    [JsonPropertyName("appid")]
    public long? AppId { get; set; }
    
    [JsonPropertyName("common")]
    public AppCommon? Common { get; set; }
    
    [JsonPropertyName("config")]
    public AppConfig? Config { get; set; }
    
    [JsonPropertyName("extended")]
    public AppExtended? Extended { get; set; }
    
    [JsonPropertyName("install")]
    public AppInstall? Install { get; set; }
    
    [JsonPropertyName("ufs")]
    public AppUfs? UFS { get; set; }
}