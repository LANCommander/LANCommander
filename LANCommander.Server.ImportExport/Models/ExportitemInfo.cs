using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ExportItemInfo
{
    public Guid Id { get; set; }
    public ImportExportRecordType Type { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}