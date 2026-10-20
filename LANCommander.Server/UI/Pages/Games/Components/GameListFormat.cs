using System.Globalization;
using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.UI.Pages.Games.Components;

/// <summary>How the Games list writes sizes, ages and the like: short, so they fit a narrow mono column.</summary>
public static class GameListFormat
{
    /// <summary>
    /// The compressed size of a game's latest archive, as the Archive column sorts it; null without one.
    /// Translated to a correlated subquery, so the column sorts in the database.
    /// </summary>
    public static readonly Expression<Func<Game, long?>> LatestArchiveSize =
        g => g.Archives!.OrderByDescending(a => a.CreatedOn).Select(a => (long?)a.CompressedSize).FirstOrDefault();

    static readonly Func<Game, long?> LatestArchiveSizeOf = LatestArchiveSize.Compile();

    /// <summary>The latest archive's size for a game whose archives were loaded; null without any, or unloaded.</summary>
    public static long? ArchiveSize(Game game) => game.Archives == null ? null : LatestArchiveSizeOf(game);

    /// <summary>"2.1 GB", "412 MB": one decimal at most, decimal units.</summary>
    public static string Size(long bytes, string format = "0.#") =>
        ByteSizeLib.ByteSize.FromBytes(bytes).ToString(format, CultureInfo.CurrentCulture);

    /// <summary>"just now", "5m ago", "3h ago", "3d ago", "2w ago", "1mo ago", "2y ago".</summary>
    public static string Ago(DateTime utc, DateTime? now = null)
    {
        var elapsed = (now ?? DateTime.UtcNow) - DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        if (elapsed < TimeSpan.FromMinutes(1))
            return "just now";

        if (elapsed < TimeSpan.FromHours(1))
            return $"{(int)elapsed.TotalMinutes}m ago";

        if (elapsed < TimeSpan.FromDays(1))
            return $"{(int)elapsed.TotalHours}h ago";

        if (elapsed < TimeSpan.FromDays(7))
            return $"{(int)elapsed.TotalDays}d ago";

        if (elapsed < TimeSpan.FromDays(30))
            return $"{(int)(elapsed.TotalDays / 7)}w ago";

        if (elapsed < TimeSpan.FromDays(365))
            return $"{Math.Max(1, (int)(elapsed.TotalDays / 30))}mo ago";

        return $"{(int)(elapsed.TotalDays / 365)}y ago";
    }

    static readonly HashSet<string> MinorWords = new(StringComparer.OrdinalIgnoreCase) { "a", "an", "and", "of", "the" };

    /// <summary>
    /// Two letters for a title's placeholder icon: the first letters of its first two words, skipping
    /// "of", "the" and the like, else its first two letters. "Age of Empires II" is "AE".
    /// </summary>
    public static string Initials(string? title)
    {
        var words = (title ?? "")
            .Split([' ', ':', '-', '_', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .ToList();

        if (words.Count > 1)
            words = words.Take(1).Concat(words.Skip(1).Where(w => !MinorWords.Contains(w))).ToList();

        var initials = words.Count switch
        {
            0 => "",
            1 => new string(words[0].Where(char.IsLetterOrDigit).Take(2).ToArray()),
            _ => $"{words[0][0]}{words[1][0]}",
        };

        return initials.ToUpperInvariant();
    }

    /// <summary>"2 expansions", "1 mod", "3 add-ons" when mixed: what an expandable row holds.</summary>
    public static string ChildSummary(IReadOnlyCollection<Game> children)
    {
        var types = children.Select(c => c.Type).Distinct().ToList();

        var noun = types.Count == 1
            ? types[0] switch
            {
                GameType.Expansion => children.Count == 1 ? "expansion" : "expansions",
                GameType.Mod => children.Count == 1 ? "mod" : "mods",
                _ => children.Count == 1 ? "add-on" : "add-ons",
            }
            : children.Count == 1 ? "add-on" : "add-ons";

        return $"{children.Count:N0} {noun}";
    }
}
