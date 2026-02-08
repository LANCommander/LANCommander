using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class Manifest
{
    [JsonPropertyName("download")]
    public string? Download { get; set; }

    [JsonPropertyName("gid")]
    public string? GID { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }
}