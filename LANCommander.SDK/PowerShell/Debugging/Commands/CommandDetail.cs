#nullable enable
using System.Collections.Generic;

namespace LANCommander.SDK.PowerShell.Debugging.Commands;

/// <summary>One parameter of a command, flattened for display.</summary>
public sealed class CommandParameterSnapshot
{
    public required string Name { get; init; }

    public required string TypeName { get; init; }

    public required bool IsMandatory { get; init; }

    /// <summary>Positional index, or null for a named-only parameter.</summary>
    public int? Position { get; init; }

    public required string Aliases { get; init; }

    public string Display => "-" + Name + (Position is { } p ? " (pos " + p + ")" : string.Empty);

    public string Annotation =>
        (IsMandatory ? "required" : "optional")
        + (Aliases.Length == 0 ? string.Empty : ", alias " + Aliases);
}

/// <summary>
/// The per-selection detail for one command: what <c>Get-Help</c> and the parameter metadata say.
/// </summary>
/// <remarks>
/// Fetched lazily rather than as part of the list. Reading <c>CommandInfo.Parameters</c> forces the
/// declaring module to be imported, so doing it for every row would import every module on PSModulePath.
/// </remarks>
public sealed class CommandDetail
{
    public required string Name { get; init; }

    public required string ModuleName { get; init; }

    public required string Synopsis { get; init; }

    public required string Syntax { get; init; }

    public required IReadOnlyList<CommandParameterSnapshot> Parameters { get; init; }

    public bool HasParameters => Parameters.Count > 0;
}
