using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.Importers;

public class ImportItemInfo
{
    public ImportRecordFlags Flag { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
}