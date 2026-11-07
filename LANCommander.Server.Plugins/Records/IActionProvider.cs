using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>Something a launcher can run for a game, tool or server, such as starting it.</summary>
public sealed record ActionInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>The executable, usually relative to <c>{InstallDir}</c>.</summary>
    public string? Path { get; init; }

    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }

    /// <summary>What the action is for, which decides where a launcher offers it.</summary>
    public ActionType Type { get; init; }

    /// <summary>Whether this is the action a launcher runs when asked to play.</summary>
    [Obsolete("Use Type. Setting this only moves the action into or out of ActionType.Primary.")]
    public bool IsPrimary
    {
        get => Type == ActionType.Primary;
        init => Type = ActionTypes.FromPrimaryFlag(value, Type);
    }

    public int SortOrder { get; init; }

    /// <summary>The platforms the action runs on.</summary>
    public RuntimePlatform Platforms { get; init; }

    /// <summary>Install option values the action overrides, as JSON.</summary>
    public string? OptionOverrides { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public RecordOwner Owner { get; init; }
}

/// <summary>
/// Manages the actions of games (per version), tools and servers. Provided by the server; inject
/// it into a scoped plugin service or page.
/// </summary>
public interface IActionProvider : IOwnedRecordProvider<ActionInfo>;
