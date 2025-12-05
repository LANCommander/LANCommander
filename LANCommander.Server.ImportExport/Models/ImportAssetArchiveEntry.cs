namespace LANCommander.Server.ImportExport.Models;

public class ImportAssetArchiveEntry : IImportAsset
{
    public Guid RecordId { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
}