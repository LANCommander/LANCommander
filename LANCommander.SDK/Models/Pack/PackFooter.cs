namespace LANCommander.SDK.Models.Pack;

public class PackFooter
{
    public const int FooterSize = 28;

    public ulong DirectoryOffset { get; set; }
    public ulong EntryCount { get; set; }
    public uint DataChecksum { get; set; }
    public uint DirectoryChecksum { get; set; }
}
