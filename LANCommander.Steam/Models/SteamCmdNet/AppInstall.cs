using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppInstall
{
    [JsonPropertyName("registry")]
    public RegistryRoot? Registry { get; set; }

    [JsonPropertyName("utf8_registry_strings")]
    public string? UTF8RegistryStrings { get; set; }
}