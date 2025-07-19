using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.Importers;

public class ExportItemInfo
{
    public ImportRecordFlags Flag { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}