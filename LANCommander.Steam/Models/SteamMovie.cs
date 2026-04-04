using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models;

public class SteamMovie
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public SteamMovieAsset? Webm { get; set; }
    public SteamMovieAsset? Mp4 { get; set; }
    public bool Highlight { get; set; }
}

public class SteamMovieAsset
{
    [JsonPropertyName("480")]
    public string? Resolution480 { get; set; }

    [JsonPropertyName("max")]
    public string? Max { get; set; }
}
