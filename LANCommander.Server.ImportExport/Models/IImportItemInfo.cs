using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public interface IImportItemInfo
{
    string Key { get; set; }
    ImportExportRecordType Type { get; set; }
    string Name { get; set; }
    bool Processed { get; set; }
    long Size { get; set; }
}