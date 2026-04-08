using System.Collections.Generic;

namespace LANCommander.SDK.Models.Pack;

public class PackManifest
{
    public PackHeader Header { get; set; } = new();
    public List<PackDirectoryEntry> Entries { get; set; } = [];
    public PackFooter Footer { get; set; } = new();
}
