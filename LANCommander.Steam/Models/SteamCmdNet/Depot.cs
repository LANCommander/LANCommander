using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class Depot
{
    [JsonPropertyName("config")]
    public DepotConfig? Config { get; set; }

    [JsonPropertyName("depotfromapp")]
    public string? DepotFromApp { get; set; }

    [JsonPropertyName("sharedinstall")]
    public string? SharedInstall { get; set; }

    [JsonPropertyName("dlcappid")]
    public string? DlcAppId { get; set; }

    [JsonPropertyName("systemdefined")]
    public string? SystemDefined { get; set; }

    [JsonPropertyName("manifests")]
    public Dictionary<string, Manifest>? Manifests { get; set; }
}