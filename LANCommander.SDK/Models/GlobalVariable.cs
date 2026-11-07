namespace LANCommander.SDK.Models;

/// <summary>
/// A value an administrator defines once on the server and every script (game, redistributable,
/// server and tool alike) is run with as a PowerShell variable of the same name.
/// </summary>
public class GlobalVariable
{
    public string Name { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
}
