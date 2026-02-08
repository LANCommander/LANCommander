using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppInfoResponse
{
    [JsonPropertyName("data")]
    public Dictionary<uint, AppInfo>? Data { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}