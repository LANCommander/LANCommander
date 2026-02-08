using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppUfs
{
    [JsonPropertyName("maxnumfiles")]
    public string? MaxNumFiles { get; set; }

    [JsonPropertyName("quota")]
    public string? Quota { get; set; }
}