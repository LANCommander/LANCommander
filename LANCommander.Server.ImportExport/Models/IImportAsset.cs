namespace LANCommander.Server.ImportExport.Models;

public interface IImportAsset
{
    public Guid RecordId { get; set; }
    public string Name { get; set; }
}