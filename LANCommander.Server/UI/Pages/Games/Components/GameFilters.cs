using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Pages.Games.Components;

/// <summary>The relations the Games list can be narrowed by; each maps to a relation the table already includes.</summary>
public enum GameFacet
{
    Collection,
    Platform,
    Genre,
    Developer,
    Publisher,
    Engine,
    Tag,
    MultiplayerMode,
    Type,

    /// <summary>Games lacking a piece of artwork or text, e.g. a cover; values are <see cref="GameFilters.MissingValues"/>.</summary>
    Missing,
}

/// <summary>How a view's dot is coloured in the rail: what kind of attention its games need.</summary>
public enum GameViewTone
{
    None,
    Neutral,
    Warning,
    Danger,
}

/// <summary>A named filter over the whole library, e.g. "Missing art".</summary>
public sealed record GameView(string Key, string Name, IconType Icon, Expression<Func<Game, bool>>? Predicate, GameViewTone Tone = GameViewTone.Neutral);

/// <summary>
/// What the Games list is narrowed to: an optional view, and per facet the values any of which a
/// game must have. Facets combine with AND, values within a facet with OR. Immutable, so every
/// change is a new instance and the table sees a new query.
/// </summary>
public sealed record GameFilters
{
    public const string AllGames = "all";

    /// <summary>The rows the Games page lists at all: base games, with expansions and mods under them.</summary>
    public static readonly Expression<Func<Game, bool>> Listed =
        g => g.BaseGameId == null || g.BaseGameId == Guid.Empty || (g.Type != GameType.Expansion && g.Type != GameType.Mod);

    /// <summary>The <see cref="GameFacet.Missing"/> values, in the order the rail lists them.</summary>
    public static IReadOnlyList<string> MissingValues { get; } = ["Cover", "Icon", "Background", "Logo", "Description"];

    /// <summary>The artwork the Missing art view looks for: a game lacking any of these is in it.</summary>
    static readonly MediaType[] ArtTypes = [MediaType.Cover, MediaType.Icon, MediaType.Background, MediaType.Logo];

    // After MissingValues and ArtTypes: static initializers run in order, and these use them
    public static IReadOnlyList<GameView> Views { get; } =
    [
        new(AllGames, "All games", IconType.GameController, null, GameViewTone.None),
        new("missing-art", "Missing art", IconType.ImageBroken, MissingArt(), GameViewTone.Warning),
        new("no-archive", "No archive", IconType.FileDashed, g => !g.Archives!.Any(), GameViewTone.Danger),
        new("no-scripts", "No scripts", IconType.Code, g => !g.Scripts!.Any(), GameViewTone.Warning),
        new("added-this-week", "Added this week", IconType.CalendarPlus, AddedThisWeek()),
        new("hidden", "Hidden from depot", IconType.EyeSlash, g => !g.Published),
    ];

    public string View { get; init; } = AllGames;

    public IReadOnlyDictionary<GameFacet, IReadOnlySet<string>> Facets { get; init; } = new Dictionary<GameFacet, IReadOnlySet<string>>();

    public bool IsEmpty => View == AllGames && Facets.All(f => f.Value.Count == 0);

    public bool Has(GameFacet facet, string value) => Facets.TryGetValue(facet, out var values) && values.Contains(value);

    public GameFilters Toggle(GameFacet facet, string value)
    {
        var values = Facets.TryGetValue(facet, out var current) ? new HashSet<string>(current) : new HashSet<string>();

        if (!values.Remove(value))
            values.Add(value);

        return this with { Facets = new Dictionary<GameFacet, IReadOnlySet<string>>(Facets) { [facet] = values } };
    }

    public GameFilters WithView(string view) => this with { View = view };

    /// <summary>The same view with no facet values chosen.</summary>
    public GameFilters WithoutFacets() => this with { Facets = new Dictionary<GameFacet, IReadOnlySet<string>>() };

    /// <summary>How many values are chosen for <paramref name="facet"/>.</summary>
    public int CountOf(GameFacet facet) => Facets.TryGetValue(facet, out var values) ? values.Count : 0;

    /// <summary>The list's query: listed games, in the view, matching every facet.</summary>
    public Expression<Func<Game, bool>> ToExpression()
    {
        var predicate = Listed;

        var view = Views.FirstOrDefault(v => v.Key == View)?.Predicate;

        if (view != null)
            predicate = And(predicate, view);

        foreach (var (facet, values) in Facets)
        {
            if (values.Count > 0)
                predicate = And(predicate, Matches(facet, values));
        }

        return predicate;
    }

    /// <summary>Games having any of <paramref name="values"/> for <paramref name="facet"/>.</summary>
    public static Expression<Func<Game, bool>> Matches(GameFacet facet, IEnumerable<string> values)
    {
        var ids = values.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList();

        switch (facet)
        {
            case GameFacet.Collection: return g => g.Collections.Any(c => ids.Contains(c.Id));
            case GameFacet.Platform: return g => g.Platforms!.Any(p => ids.Contains(p.Id));
            case GameFacet.Genre: return g => g.Genres!.Any(x => ids.Contains(x.Id));
            case GameFacet.Developer: return g => g.Developers!.Any(c => ids.Contains(c.Id));
            case GameFacet.Publisher: return g => g.Publishers!.Any(c => ids.Contains(c.Id));
            case GameFacet.Engine: return g => g.EngineId != null && ids.Contains(g.EngineId.Value);
            case GameFacet.Tag: return g => g.Tags!.Any(t => ids.Contains(t.Id));

            case GameFacet.MultiplayerMode:
                var modes = values.Select(v => Enum.TryParse<MultiplayerType>(v, out var mode) ? mode : (MultiplayerType?)null)
                    .Where(m => m != null).Select(m => m!.Value).ToList();

                return g => g.MultiplayerModes!.Any(m => modes.Contains(m.Type));

            case GameFacet.Type:
                var types = values.Select(v => Enum.TryParse<GameType>(v, out var type) ? type : (GameType?)null)
                    .Where(t => t != null).Select(t => t!.Value).ToList();

                return g => types.Contains(g.Type);

            case GameFacet.Missing:
                // Any of the chosen gaps, like every other facet's values
                var missing = values.Select(MissingPredicate).Where(p => p != null).Select(p => p!).ToList();

                return missing.Count == 0 ? _ => true : missing.Aggregate(Or);

            default:
                throw new ArgumentOutOfRangeException(nameof(facet), facet, null);
        }
    }

    /// <summary>Games lacking the given <see cref="MissingValues"/> entry, e.g. "Cover"; null for an unknown one.</summary>
    public static Expression<Func<Game, bool>>? MissingPredicate(string value)
    {
        if (value == "Description")
            return g => g.Description == null || g.Description.Trim() == "";

        if (!MissingValues.Contains(value) || !Enum.TryParse<MediaType>(value, out var type))
            return null;

        return g => !g.Media!.Any(m => m.Type == type);
    }

    static Expression<Func<Game, bool>> MissingArt() =>
        ArtTypes.Select(type => MissingPredicate(type.ToString())!).Aggregate(Or);

    public static Expression<Func<Game, bool>> And(Expression<Func<Game, bool>> left, Expression<Func<Game, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    public static Expression<Func<Game, bool>> Or(Expression<Func<Game, bool>> left, Expression<Func<Game, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    static Expression<Func<Game, bool>> Combine(Expression<Func<Game, bool>> left, Expression<Func<Game, bool>> right, Func<Expression, Expression, BinaryExpression> combine)
    {
        var parameter = left.Parameters[0];
        var body = new ReplaceParameter(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<Game, bool>>(combine(left.Body, body), parameter);
    }

    static Expression<Func<Game, bool>> AddedThisWeek()
    {
        // Evaluated when the query runs, not when the view list was built
        return g => g.CreatedOn >= DateTime.UtcNow.AddDays(-7);
    }

    sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>A filter someone named and kept, stored in the browser beside the table's other settings.</summary>
public sealed record SavedGameView(string Name, string View, Dictionary<GameFacet, List<string>> Facets)
{
    public static SavedGameView From(string name, GameFilters filters) =>
        new(name, filters.View, filters.Facets.Where(f => f.Value.Count > 0).ToDictionary(f => f.Key, f => f.Value.ToList()));

    public GameFilters ToFilters() => new()
    {
        View = View,
        Facets = Facets.ToDictionary(f => f.Key, f => (IReadOnlySet<string>)f.Value.ToHashSet()),
    };
}
