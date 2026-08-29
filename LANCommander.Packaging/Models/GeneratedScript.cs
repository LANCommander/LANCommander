using LANCommander.SDK.Enums;

namespace LANCommander.Packaging.Models;

/// <summary>
/// A PowerShell script produced from the captured changes.
/// </summary>
public class GeneratedScript
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ScriptType Type { get; set; }

    public string Contents { get; set; } = string.Empty;

    /// <summary>Set when the script touches HKLM and therefore needs elevation to run.</summary>
    public bool RequiresAdmin { get; set; }
}
