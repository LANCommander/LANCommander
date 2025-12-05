using LANCommander.Server.ImportExport.Legacy.Enums;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class Media : BaseModel
{
    public Guid FileId { get; set; }
    public string Name { get; set; }
    public MediaType Type { get; set; }
    public string SourceUrl { get; set; }
    public string MimeType { get; set; }
    public string Crc32 { get; set; }
}