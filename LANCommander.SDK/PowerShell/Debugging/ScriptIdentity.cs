#nullable enable
using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>What a script belongs to.</summary>
public enum ScriptOwnerKind
{
    Game,
    Redistributable,
    Tool,
}

/// <summary>
/// Identifies a script independently of where it is stored: the id of the game, addon, redistributable or
/// tool that owns it, plus its type. Breakpoints are keyed by this, so a breakpoint set on a server copy
/// or a draft still applies once the script is written into an install directory.
/// </summary>
public readonly record struct ScriptKey(Guid OwnerId, ScriptType Type);

/// <summary>
/// Everything known about a script that is about to run: its key, what kind of thing owns it, the game it
/// is running for (null for tool scripts that run outside a game), the install directory it runs in, and
/// the file it was loaded from.
/// </summary>
public sealed record ScriptIdentity(
    ScriptKey Key,
    ScriptOwnerKind OwnerKind,
    Guid? GameId,
    string? InstallDirectory,
    string? ScriptPath);
