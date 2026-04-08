using System;

namespace LANCommander.SDK.Models.Pack;

public class PackOptions
{
    public PackCompression Compression { get; set; } = PackCompression.None;
    public bool WriteDirectory { get; set; } = true;
    public Guid PackId { get; set; } = Guid.NewGuid();
    public Guid ParentPackId { get; set; } = Guid.Empty;
    public string PackVersion { get; set; } = string.Empty;
    public string ParentVersion { get; set; } = string.Empty;
}
