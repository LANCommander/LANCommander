using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.Services.ScriptDebugging;

/// <summary>Where the text shown for a script came from.</summary>
public enum ScriptSource
{
    /// <summary>The file in the install directory that actually runs.</summary>
    Installed,

    /// <summary>The server's copy; the game is not installed.</summary>
    Server,

    /// <summary>A local edit of the server's copy, waiting to be written in when the game is installed.</summary>
    Draft,
}

/// <summary>Every script a game's debugger window shows, grouped by what owns them.</summary>
public sealed record ScriptWorkspace(
    Guid GameId,
    string Title,
    string? InstallDirectory,
    bool IsInstalled,
    IReadOnlyList<ScriptOwnerNode> Owners,
    string? Message = null)
{
    public IEnumerable<ScriptEntry> Scripts => Owners.SelectMany(o => o.Scripts);

    /// <summary>Ids of everything whose scripts this workspace shows.</summary>
    public IReadOnlySet<Guid> OwnerIds { get; } = Owners.Select(o => o.Id).ToHashSet();
}

/// <summary>A game, addon, redistributable or tool, and its scripts.</summary>
public sealed record ScriptOwnerNode(
    ScriptOwnerKind Kind,
    Guid Id,
    string Name,
    bool IsAddon,
    IReadOnlyList<ScriptEntry> Scripts);

/// <summary>One script as the debugger window sees it.</summary>
public sealed record ScriptEntry
{
    public required ScriptKey Key { get; init; }

    public required ScriptOwnerKind OwnerKind { get; init; }

    public required string OwnerName { get; init; }

    /// <summary>The server's id for the script, when known. Needed to upload edits.</summary>
    public Guid? ServerScriptId { get; init; }

    public required string Name { get; init; }

    public bool RequiresAdmin { get; init; }

    public required ScriptSource Source { get; init; }

    /// <summary>The file in the install directory, for installed games.</summary>
    public string? LocalPath { get; init; }

    /// <summary>Where a draft of this script is (or would be) kept.</summary>
    public required string DraftPath { get; init; }

    /// <summary>The server's contents, for scripts listed from the server.</summary>
    public string? ServerContents { get; init; }

    public ScriptType Type => Key.Type;

    /// <summary>
    /// The path breakpoints are keyed against in the editor: the installed file when there is one,
    /// otherwise the draft path. The engine always rebinds them to the file that actually runs.
    /// </summary>
    public string EditorPath => LocalPath ?? DraftPath;

    /// <summary>
    /// RunWrapper scripts wrap the game's executable and only make sense when the game is played, and
    /// every other script needs an install directory to run in.
    /// </summary>
    public bool CanRunDirectly => Source == ScriptSource.Installed && Type != ScriptType.RunWrapper;
}
