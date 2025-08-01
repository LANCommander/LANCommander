using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ImportQueueItem
{
    public Guid Id { get; set; }
    public ImportExportRecordType Type { get; set; }
    public object Record { get; set; }
    public bool Processed { get; set; }

    public ImportQueueItem(ImportExportRecordType type, object record)
    {
        Type = type;
        Record = record;
    }

    public ImportQueueItem(Guid id, ImportExportRecordType type, object record)
    {
        Type = type;
        Record = record;
    }
}