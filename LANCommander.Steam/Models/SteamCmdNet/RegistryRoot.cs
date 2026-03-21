using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class RegistryRoot
{
    // Keys look like: "hkey_local_machine\\software\\valve\\cs2"
    [JsonExtensionData]
    public Dictionary<string, object>? Keys { get; set; }
}