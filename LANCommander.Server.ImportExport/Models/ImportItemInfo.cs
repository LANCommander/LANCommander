using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ImportItemInfo
{
    public bool Selected { get; set; }
    public ImportRecordFlags Flag { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}