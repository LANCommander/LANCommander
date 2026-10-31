using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

public class ExportItemInfo
{
    public Guid Id { get; set; }
    public ImportExportRecordType Type { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }

    /// <summary>When the record was last changed, or null for records that don't track it.</summary>
    public DateTime? UpdatedOn { get; set; }
}