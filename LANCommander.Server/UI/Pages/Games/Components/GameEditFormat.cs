using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.UI.Pages.Games.Components;

/// <summary>The short texts of the game editor's header and Preview: summaries, counts and kickers.</summary>
public static class GameEditFormat
{
    /// <summary>
    /// The mono line beside a game's title: "v338.4 · 4.70 GB". Either part may be missing; null
    /// when both are. A version that already starts with "v" isn't given a second one.
    /// </summary>
    public static string? Summary(string? version, long? size)
    {
        var parts = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(version))
        {
            version = version.Trim();

            parts.Add(version.Length > 1 && version[0] is 'v' or 'V' && char.IsDigit(version[1]) ? version : $"v{version}");
        }

        if (size is > 0)
            parts.Add(GameListFormat.Size(size.Value, "0.00"));

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    /// <summary>"1 script", "5 scripts", "2 redistributables".</summary>
    public static string Count(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>
    /// The Preview's media kicker: "Media · 6 screenshots, 1 video", leaving out a kind there is
    /// none of; just "Media" when there are neither. Shown in capitals by the kicker style.
    /// </summary>
    public static string MediaKicker(int screenshots, int videos)
    {
        var parts = new List<string>(2);

        if (screenshots > 0)
            parts.Add(Count(screenshots, "screenshot"));

        if (videos > 0)
            parts.Add(Count(videos, "video"));

        return parts.Count > 0 ? $"Media · {string.Join(", ", parts)}" : "Media";
    }
}

/// <summary>
/// The fields the General section edits, captured so the editor can tell whether anything changed
/// since the game was loaded or saved. Compares by value: taxonomies by id regardless of order,
/// text ignoring the difference between empty and missing and between line-ending styles (the
/// markdown editor rewrites both when it loads).
/// </summary>
public sealed record GameEditSnapshot(
    string? Title,
    string? SortTitle,
    DateTime? ReleasedOn,
    GameType Type,
    Guid? BaseGameId,
    GameInstallLocation InstallTo,
    string? DirectoryName,
    bool ShowInLibrary,
    Guid? EngineId,
    KeyAllocationMethod KeyAllocationMethod,
    bool Singleplayer,
    bool Published,
    string? Description,
    string? Notes,
    string Developers,
    string Publishers,
    string Platforms,
    string Genres,
    string Tags,
    string Collections,
    string ExternalIds)
{
    public static GameEditSnapshot Of(Game game) => new(
        Text(game.Title),
        Text(game.SortTitle),
        game.ReleasedOn,
        game.Type,
        game.BaseGameId,
        game.InstallTo,
        Text(game.DirectoryName),
        game.ShowInLibrary,
        game.EngineId,
        game.KeyAllocationMethod,
        game.Singleplayer,
        game.Published,
        Text(game.Description),
        Text(game.Notes),
        Keys(game.Developers),
        Keys(game.Publishers),
        Keys(game.Platforms),
        Keys(game.Genres),
        Keys(game.Tags),
        Keys(game.Collections),
        string.Join("\n", game.ExternalIds?.Select(e => $"{Text(e.Provider)}\u001f{Text(e.ExternalId)}") ?? []));

    static string? Text(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.Replace("\r\n", "\n");

    static string Keys<T>(IEnumerable<T>? items) where T : BaseTaxonomyModel =>
        string.Join("\n", (items ?? []).Select(i => i.Id != Guid.Empty ? i.Id.ToString() : i.Name).Order(StringComparer.Ordinal));
}
