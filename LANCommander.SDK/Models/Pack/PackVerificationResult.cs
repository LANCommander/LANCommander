using System.Collections.Generic;

namespace LANCommander.SDK.Models.Pack;

public class PackVerificationResult
{
    public bool IsValid { get; set; }
    public List<PackVerificationFailure> Failures { get; set; } = [];
}

public class PackVerificationFailure
{
    public string Path { get; set; } = string.Empty;
    public uint ExpectedChecksum { get; set; }
    public uint ActualChecksum { get; set; }
}
