using LANCommander.SDK.Enums;

namespace LANCommander.Server.Models;

public class ExportDialogOptions
{
    public ImportExportRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
}