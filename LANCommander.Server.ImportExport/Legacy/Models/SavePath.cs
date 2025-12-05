using LANCommander.Server.ImportExport.Legacy.Enums;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class SavePath : KeyedModel
{
    public SavePathType Type { get; set; }
    public string Path { get; set; }
    public string WorkingDirectory { get; set; }
    public bool IsRegex { get; set; }
    public IEnumerable<SavePathEntry> Entries { get; set; }
}