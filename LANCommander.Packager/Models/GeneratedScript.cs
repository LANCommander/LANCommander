using LANCommander.SDK.Enums;

namespace LANCommander.Packager.Models;

public class GeneratedScript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScriptType Type { get; set; }
    public string Contents { get; set; } = string.Empty;
    public bool RequiresAdmin { get; set; }
}
