using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ImportItemInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ImportExportRecordType Type { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}