namespace LANCommander.Server.ImportExport.Models;

public class ImportAssetText : IImportAsset
{
    public Guid RecordId { get; set; }
    public string Name { get; set; }
    public string Contents { get; set; }
}