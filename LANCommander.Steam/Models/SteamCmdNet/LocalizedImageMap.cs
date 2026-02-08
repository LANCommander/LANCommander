using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public class LocalizedImageMap
{
    [JsonExtensionData]
    public Dictionary<string, object>? Values { get; set; }
}