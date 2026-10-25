using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>A PowerShell script run at a point in the life of a game, redistributable, tool or server.</summary>
public sealed record ScriptInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }

    /// <summary>When the script runs, e.g. on install.</summary>
    public ScriptType Type { get; init; }

    public string Contents { get; init; } = "";
    public bool RequiresAdmin { get; init; }

    /// <summary>The platforms the script runs on.</summary>
    public RuntimePlatform Platforms { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public RecordOwner Owner { get; init; }
}

/// <summary>
/// Manages the scripts of games (per version), redistributables, tools and servers. Provided by
/// the server; inject it into a scoped plugin service or page.
/// </summary>
/// <remarks>
/// Scripts of type <see cref="ScriptType.Package"/> build packages on the server and are neither
/// listed nor accepted.
/// </remarks>
public interface IScriptProvider : IOwnedRecordProvider<ScriptInfo>;
