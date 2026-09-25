#nullable enable

namespace LANCommander.SDK.PowerShell.Debugging.Commands;

/// <summary>
/// One row of the command list, flattened at capture time.
/// </summary>
/// <remarks>
/// Nothing here holds a live <c>CommandInfo</c>: a live command object keeps its owning session state
/// alive, and reading a property on one can import a module. Details are fetched separately, on demand.
/// </remarks>
public sealed class CommandInfoSnapshot
{
    public required string Name { get; init; }

    /// <summary>"Cmdlet", "Function" or "Alias".</summary>
    public required string CommandType { get; init; }

    /// <summary>The declaring module, or empty for commands that belong to no module.</summary>
    public required string ModuleName { get; init; }

    public required string Version { get; init; }

    public string ModuleDisplay => ModuleName.Length == 0 ? "-" : ModuleName;
}
