using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.UI.Controls;

/// <summary>What owns a group of scripts in the script editor's list.</summary>
public enum ScriptOwnerKind
{
    Game,
    Redistributable,
    Server,
    Tool,
    System,
}

/// <summary>One owner's scripts in the script editor's list: the game, a redistributable it depends on, the system.</summary>
public sealed record ScriptEditorGroup(string Key, string Name, ScriptOwnerKind Kind, IReadOnlyList<Script> Scripts)
{
    /// <summary>The owner kind as the list labels it ("GAME", "REDIST"); the system group has none.</summary>
    public string? KindLabel => Kind switch
    {
        ScriptOwnerKind.Game => "GAME",
        ScriptOwnerKind.Redistributable => "REDIST",
        ScriptOwnerKind.Server => "SERVER",
        ScriptOwnerKind.Tool => "TOOL",
        _ => null,
    };
}

/// <summary>
/// Groups the scripts a script editor lists by their owner: the script's own owner first, then
/// the redistributables (by name), servers and tools, and the system scripts last.
/// </summary>
public static class ScriptEditorGroups
{
    public static List<ScriptEditorGroup> Build(IEnumerable<Script> scripts, Script current)
    {
        var currentKey = KeyOf(current);

        return scripts
            .GroupBy(KeyOf)
            .Select(group => new ScriptEditorGroup(
                group.Key,
                NameOf(group.First()),
                KindOf(group.First()),
                group.OrderBy(s => s.Type).ThenBy(s => s.Name).ToList()))
            .OrderBy(group => group.Key == currentKey ? 0 : group.Kind == ScriptOwnerKind.System ? 2 : 1)
            .ThenBy(group => group.Kind)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ScriptOwnerKind KindOf(Script script) =>
        Has(script.GameId) ? ScriptOwnerKind.Game
        : Has(script.RedistributableId) ? ScriptOwnerKind.Redistributable
        : Has(script.ServerId) ? ScriptOwnerKind.Server
        : Has(script.ToolId) ? ScriptOwnerKind.Tool
        : ScriptOwnerKind.System;

    public static string KeyOf(Script script) => KindOf(script) switch
    {
        ScriptOwnerKind.Game => $"game:{script.GameId}",
        ScriptOwnerKind.Redistributable => $"redistributable:{script.RedistributableId}",
        ScriptOwnerKind.Server => $"server:{script.ServerId}",
        ScriptOwnerKind.Tool => $"tool:{script.ToolId}",
        _ => "system",
    };

    static string NameOf(Script script) => KindOf(script) switch
    {
        ScriptOwnerKind.Game => script.Game?.Title ?? "Game",
        ScriptOwnerKind.Redistributable => script.Redistributable?.Name ?? "Redistributable",
        ScriptOwnerKind.Server => script.Server?.Name ?? "Server",
        ScriptOwnerKind.Tool => script.Tool?.Name ?? "Tool",
        _ => "System",
    };

    static bool Has(Guid? id) => id is { } value && value != Guid.Empty;

    /// <summary>A script type as a row's tag says it: short enough to sit beside a name.</summary>
    public static string TypeLabel(ScriptType type) => type switch
    {
        ScriptType.DetectInstall => "Detect",
        ScriptType.NameChange => "Name",
        ScriptType.KeyChange => "Key",
        ScriptType.SaveUpload => "Save up",
        ScriptType.SaveDownload => "Save down",
        ScriptType.BeforeStart => "Pre-start",
        ScriptType.AfterStop => "Post-stop",
        ScriptType.GameStarted => "Started",
        ScriptType.GameStopped => "Ended",
        ScriptType.UserRegistration => "Register",
        ScriptType.UserLogin => "Login",
        ScriptType.ApplicationStart => "App start",
        ScriptType.RunWrapper => "Wrapper",
        _ => type.ToString(),
    };
}
