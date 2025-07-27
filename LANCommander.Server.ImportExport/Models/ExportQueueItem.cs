using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ExportQueueItem
{
    public Guid Id { get; set; }
    public ExportRecordFlags Type { get; set; }
    public object Record { get; set; }
    public bool Processed { get; set; }

    public ExportQueueItem(ExportRecordFlags type, object record)
    {
        Type = type;
        Record = record;
    }

    public ExportQueueItem(Guid id, ExportRecordFlags type, object record)
    {
        Type = type;
        Record = record;
    }
}