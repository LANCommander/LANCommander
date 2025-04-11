using LANCommander.SDK.Enums;

namespace LANCommander.Server.Models;

public class ExportDialogOptions
{
    public ExportRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
}