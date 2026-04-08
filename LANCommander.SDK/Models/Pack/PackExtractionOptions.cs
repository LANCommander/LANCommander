namespace LANCommander.SDK.Models.Pack;

public class PackExtractionOptions
{
    public bool VerifyChecksums { get; set; } = true;
    public bool OverwriteExisting { get; set; } = true;
    public bool PreserveTimestamps { get; set; } = true;
    public bool SkipUnchangedFiles { get; set; } = true;
}
