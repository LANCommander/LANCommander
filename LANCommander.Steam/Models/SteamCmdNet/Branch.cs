using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class Branch
{
    [JsonPropertyName("buildid")]
    public string? BuildId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("timeupdated")]
    public string? TimeUpdated { get; set; }
}