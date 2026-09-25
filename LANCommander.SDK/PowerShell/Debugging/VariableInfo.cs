#nullable enable

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// A variable or property, flattened to display strings at capture time.
/// </summary>
/// <remarks>
/// Values are never held as live <c>PSObject</c>s past the stop, since they would mutate underneath the
/// UI once the script resumes. Children are fetched lazily through the pump using <see cref="Handle"/>,
/// which indexes a table that the session clears on resume.
/// </remarks>
public sealed class VariableInfo
{
    public required string Name { get; init; }

    public required string TypeName { get; init; }

    public required string Value { get; init; }

    public required bool HasChildren { get; init; }

    /// <summary>Key into the session's per-stop handle table, or null if this node cannot be expanded.</summary>
    public int? Handle { get; init; }
}
