using LANCommander.Server.ImportExport.Legacy.Enums;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class Script : BaseModel
{
    public ScriptType Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool RequiresAdmin { get; set; }
    public string Contents { get; set; }
}