using LANCommander.SDK.Enums;

namespace LANCommander.Server.Models;

public class ImportDialogOptions
{
    public string Hint { get; set; }
    public ManifestType? ManifestType { get; set; }
}