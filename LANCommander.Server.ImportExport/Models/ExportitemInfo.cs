using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ExportItemInfo
{
    public Guid Id { get; set; }
    public bool Selected { get; set; }
    public ExportRecordFlags Flag { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}