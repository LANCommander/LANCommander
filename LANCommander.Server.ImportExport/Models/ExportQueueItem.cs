using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ExportQueueItem
{
    public Guid Id { get; set; }
    public ImportExportRecordType Type { get; set; }
    public object Record { get; set; }
    public bool Processed { get; set; }

    public ExportQueueItem(ImportExportRecordType type, object record)
    {
        Type = type;
        Record = record;
    }

    public ExportQueueItem(Guid id, ImportExportRecordType type, object record)
    {
        Type = type;
        Record = record;
    }
}