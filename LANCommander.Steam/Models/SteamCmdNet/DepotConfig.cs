using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class DepotConfig
{
    [JsonPropertyName("oslist")]
    public string? OperatingSystemList { get; set; }

    [JsonPropertyName("osarch")]
    public string? OperatingSystemArchitecture { get; set; }

    [JsonPropertyName("optionaldlc")]
    public string? OptionalDlc { get; set; }
}