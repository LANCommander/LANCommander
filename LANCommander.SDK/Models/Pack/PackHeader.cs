using System;

namespace LANCommander.SDK.Models.Pack;

public class PackHeader
{
    public const string Magic = "LCPK";
    public const int MagicSize = 4;
    public const int VersionFieldSize = 32;
    public const int HeaderSize = 112; // Magic(4) + Version(2) + Flags(2) + EntryCount(8) + PackId(16) + ParentPackId(16) + PackVersion(32) + ParentVersion(32)

    public ushort Version { get; set; } = 2;
    public PackFlags Flags { get; set; } = PackFlags.None;
    public ulong EntryCount { get; set; }
    public Guid PackId { get; set; } = Guid.Empty;
    public Guid ParentPackId { get; set; } = Guid.Empty;
    public string PackVersion { get; set; } = string.Empty;
    public string ParentVersion { get; set; } = string.Empty;
}
