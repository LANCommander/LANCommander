using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LaunchEntry
{
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("config")]
    public LaunchConfig? Config { get; set; }

    [JsonPropertyName("executable")]
    public string? Executable { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("description_loc")]
    public Dictionary<string, string>? DescriptionLoc { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}