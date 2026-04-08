namespace LANCommander.SDK.Models.Pack;

public class PackDirectoryEntry
{
    public string Path { get; set; } = string.Empty;
    public PackEntryOperation Operation { get; set; } = PackEntryOperation.Create;
    public ulong Offset { get; set; }
    public ulong UncompressedSize { get; set; }
    public ulong CompressedSize { get; set; }
    public uint Checksum { get; set; }
}
