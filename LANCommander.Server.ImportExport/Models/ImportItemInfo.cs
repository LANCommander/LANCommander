using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ImportItemInfo<T> : IImportItemInfo where T : class
{
    public string Key { get; set; }
    public ImportExportRecordType Type { get; set; }
    public string Name { get; set; }
    public bool Processed { get; set; }
    public long Size { get; set; }
    public T Record { get; set; }
}