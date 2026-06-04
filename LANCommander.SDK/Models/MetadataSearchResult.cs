using System.Collections.Generic;

namespace LANCommander.SDK.Models;

public class MetadataSearchResult
{
    public string Id { get; set; } = string.Empty;
    public Manifest.Game Data { get; set; } = new();
}

public class MetadataSearchResultsCollection
{
    public ICollection<MetadataSearchResult> Results { get; set; } = new List<MetadataSearchResult>();
    public bool More { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

public class MetadataSubProvider
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
