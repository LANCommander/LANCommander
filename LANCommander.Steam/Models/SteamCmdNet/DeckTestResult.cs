using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class DeckTestResult
{
    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}