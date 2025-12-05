namespace LANCommander.Server.ImportExport.Models;

public class ImportAssetExternalDownload : IImportAsset
{
    public Guid RecordId { get; set; }
    public string Name { get; set; }
    public string SourceUrl { get; set; }
}