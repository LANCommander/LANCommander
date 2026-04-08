namespace LANCommander.SDK.Models.Pack;

public class PackEntryHeader
{
    public string Path { get; set; } = string.Empty;
    public PackEntryOperation Operation { get; set; } = PackEntryOperation.Create;
    public PackCompression Compression { get; set; } = PackCompression.None;
    public uint Attributes { get; set; }
    public long Timestamp { get; set; }
    public ulong UncompressedSize { get; set; }
    public ulong CompressedSize { get; set; }
    public uint Checksum { get; set; }
}
