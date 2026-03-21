using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class LaunchConfig
{
    [JsonPropertyName("osarch")]
    public string? OperatingSystemArchitecture { get; set; }

    [JsonPropertyName("oslist")]
    public string? OperatingSystemList { get; set; }

    [JsonPropertyName("realm")]
    public string? Realm { get; set; }

    [JsonPropertyName("betakey")]
    public string? BetaKey { get; set; }

    [JsonPropertyName("ownsdlc")]
    public string? OwnsDlc { get; set; }
}