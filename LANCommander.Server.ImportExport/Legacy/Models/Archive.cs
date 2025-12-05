namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class Archive : BaseModel
{
    public string Changelog { get; set; }

    public string ObjectKey { get; set; }

    public string Version { get; set; }

    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }
}