using LANCommander.SDK.Enums;

namespace LANCommander.Server.Models;

public class ImportDialogOptions
{
    public string Hint { get; set; }
    public ManifestType? ManifestType { get; set; }

    /// <summary>
    /// When set, the import dialog skips the upload stage and goes directly to record selection
    /// using this pre-uploaded archive's object key.
    /// </summary>
    public string? PreUploadedObjectKey { get; set; }
}