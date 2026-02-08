using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class SteamDeckCompatibility
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [JsonPropertyName("configuration")]
    public Dictionary<string, string>? Configuration { get; set; }
    
    [JsonPropertyName("steamos_compatibility")]
    public string? SteamOsCompatibility { get; set; }
    
    [JsonPropertyName("steamos_tests")]
    public Dictionary<string, DeckTestResult>? SteamOsTests { get; set; }

    [JsonPropertyName("test_timestamp")]
    public string? TestTimestamp { get; set; }

    [JsonPropertyName("tested_build_id")]
    public string? TestedBuildId { get; set; }

    [JsonPropertyName("tests")]
    public Dictionary<string, DeckTestResult>? Tests { get; set; }
}