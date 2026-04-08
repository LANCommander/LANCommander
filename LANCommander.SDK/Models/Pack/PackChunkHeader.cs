namespace LANCommander.SDK.Models.Pack;

public class PackChunkHeader
{
    public const string ChunkMagic = "LCPC";
    public const int ChunkHeaderSize = 16;

    public uint ChunkIndex { get; set; }
    public uint TotalChunks { get; set; }
    public uint ParentCrc { get; set; }
}
