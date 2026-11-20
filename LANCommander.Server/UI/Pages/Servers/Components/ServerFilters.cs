using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>What the Servers list can be narrowed by.</summary>
public enum ServerFacet
{
    /// <summary>The live process status; values are <see cref="ServerProcessStatus"/> names.</summary>
    Status,

    /// <summary>The game the server hosts; values are game ids.</summary>
    Game,

    /// <summary>Local, Remote, or a plugin's engine; values are <see cref="ServerFilters.EngineKey"/>s.</summary>
    Engine,

    /// <summary>Whether and when the server starts by itself; values are <see cref="ServerAutostartMethod"/> names or <see cref="ServerFilters.AutostartOff"/>.</summary>
    Autostart,
}

/// <summary>
/// A named filter over every server, e.g. "Running". Status lives in memory rather than the
/// database, so a view's predicate is built from the latest <see cref="ServerStatusSnapshot"/>.
/// </summary>
public sealed record ServerView(string Key, string Name, IconType Icon, Func<ServerStatusSnapshot, Expression<Func<Data.Models.Server, bool>>>? Predicate, FilterViewTone Tone = FilterViewTone.Neutral)
{
    /// <summary>Whether the view's rows depend on the servers' live status.</summary>
    public bool ByStatus { get; init; }
}

/// <summary>Each server's process status as last polled, keyed by server id.</summary>
public sealed class ServerStatusSnapshot
{
    public static readonly ServerStatusSnapshot Empty = new(new Dictionary<Guid, ServerProcessStatus>());

    readonly IReadOnlyDictionary<Guid, ServerProcessStatus> _statuses;

    public ServerStatusSnapshot(IReadOnlyDictionary<Guid, ServerProcessStatus> statuses) => _statuses = statuses;

    public ServerProcessStatus Of(Guid serverId) =>
        _statuses.TryGetValue(serverId, out var status) ? status : ServerProcessStatus.Stopped;

    /// <summary>The servers in any of <paramref name="statuses"/>.</summary>
    public List<Guid> IdsIn(params ServerProcessStatus[] statuses) =>
        _statuses.Where(s => statuses.Contains(s.Value)).Select(s => s.Key).ToList();

    /// <summary>The servers not known to be in any of <paramref name="statuses"/>.</summary>
    public List<Guid> IdsNotIn(params ServerProcessStatus[] statuses) =>
        _statuses.Where(s => !statuses.Contains(s.Value)).Select(s => s.Key).ToList();

    public bool SameAs(ServerStatusSnapshot other) =>
        _statuses.Count == other._statuses.Count && _statuses.All(s => other._statuses.TryGetValue(s.Key, out var status) && status == s.Value);
}

/// <summary>
/// What the Servers list is narrowed to: an optional view, and per facet the values any of which a
/// server must have. Facets combine with AND, values within a facet with OR. Immutable, so every
/// change is a new instance and the table sees a new query.
/// </summary>
public sealed record ServerFilters
{
    public const string AllServers = "all";

    /// <summary>The view listing the servers plugins' sources list, rather than the server's own.</summary>
    public const string External = "external";

    /// <summary>The <see cref="ServerFacet.Autostart"/> value for servers that don't start by themselves.</summary>
    public const string AutostartOff = "Off";

    const string ExtensionKeyPrefix = "Extension:";

    /// <summary>The <see cref="ServerFacet.Status"/> values, in the order the rail lists them.</summary>
    public static IReadOnlyList<ServerProcessStatus> StatusValues { get; } =
        [ServerProcessStatus.Running, ServerProcessStatus.Starting, ServerProcessStatus.Stopping, ServerProcessStatus.Stopped, ServerProcessStatus.Error];

    public static IReadOnlyList<ServerView> Views { get; } =
    [
        new(AllServers, "All servers", IconType.HardDrives, null, FilterViewTone.None),
        new("running", "Running", IconType.Play, s => Within(s.IdsIn(ServerProcessStatus.Running, ServerProcessStatus.Starting)), FilterViewTone.Success) { ByStatus = true },
        new("stopped", "Stopped", IconType.Stop, s => Without(s.IdsNotIn(ServerProcessStatus.Stopped, ServerProcessStatus.Retrieving))) { ByStatus = true },
        new("errored", "Errored", IconType.Warning, s => Within(s.IdsIn(ServerProcessStatus.Error)), FilterViewTone.Danger) { ByStatus = true },
        new("autostart", "Autostart", IconType.Lightning, _ => s => s.Autostart),
        new("no-game", "No game", IconType.LinkBreak, _ => s => s.GameId == null, FilterViewTone.Warning),
        new("added-this-week", "Added this week", IconType.CalendarPlus, _ => s => s.CreatedOn >= DateTime.UtcNow.AddDays(-7)),
    ];

    public string View { get; init; } = AllServers;

    public IReadOnlyDictionary<ServerFacet, IReadOnlySet<string>> Facets { get; init; } = new Dictionary<ServerFacet, IReadOnlySet<string>>();

    public bool IsEmpty => View == AllServers && Facets.All(f => f.Value.Count == 0);

    /// <summary>Whether the rows shown change with the servers' live status.</summary>
    public bool ByStatus => CountOf(ServerFacet.Status) > 0 || Views.FirstOrDefault(v => v.Key == View)?.ByStatus == true;

    public bool Has(ServerFacet facet, string value) => Facets.TryGetValue(facet, out var values) && values.Contains(value);

    public ServerFilters Toggle(ServerFacet facet, string value)
    {
        var values = Facets.TryGetValue(facet, out var current) ? new HashSet<string>(current) : new HashSet<string>();

        if (!values.Remove(value))
            values.Add(value);

        // Facets narrow the server's own servers, so choosing one leaves the External view
        var view = View == External ? AllServers : View;

        return this with { View = view, Facets = new Dictionary<ServerFacet, IReadOnlySet<string>>(Facets) { [facet] = values } };
    }

    public ServerFilters WithView(string view) => this with { View = view };

    public ServerFilters WithoutFacets() => this with { Facets = new Dictionary<ServerFacet, IReadOnlySet<string>>() };

    public int CountOf(ServerFacet facet) => Facets.TryGetValue(facet, out var values) ? values.Count : 0;

    /// <summary>The list's query: servers in the view, matching every facet, by <paramref name="statuses"/>.</summary>
    public Expression<Func<Data.Models.Server, bool>> ToExpression(ServerStatusSnapshot statuses)
    {
        Expression<Func<Data.Models.Server, bool>> predicate = _ => true;

        var view = Views.FirstOrDefault(v => v.Key == View)?.Predicate;

        if (view != null)
            predicate = And(predicate, view(statuses));

        foreach (var (facet, values) in Facets)
        {
            if (values.Count > 0)
                predicate = And(predicate, Matches(facet, values, statuses));
        }

        return predicate;
    }

    /// <summary>Servers having any of <paramref name="values"/> for <paramref name="facet"/>.</summary>
    public static Expression<Func<Data.Models.Server, bool>> Matches(ServerFacet facet, IEnumerable<string> values, ServerStatusSnapshot statuses)
    {
        switch (facet)
        {
            case ServerFacet.Status:
                var chosen = values.Select(v => Enum.TryParse<ServerProcessStatus>(v, out var status) ? status : (ServerProcessStatus?)null)
                    .Where(s => s != null).Select(s => s!.Value).ToList();

                // Servers not polled yet count as stopped, as the status column shows them
                if (chosen.Contains(ServerProcessStatus.Stopped))
                    return Without(statuses.IdsNotIn([.. chosen, ServerProcessStatus.Retrieving]));

                return Within(statuses.IdsIn([.. chosen]));

            case ServerFacet.Game:
                var ids = values.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList();

                return s => s.GameId != null && ids.Contains(s.GameId.Value);

            case ServerFacet.Engine:
                var engines = values.Where(v => !v.StartsWith(ExtensionKeyPrefix, StringComparison.Ordinal))
                    .Select(v => Enum.TryParse<ServerEngine>(v, out var engine) ? engine : (ServerEngine?)null)
                    .Where(e => e != null).Select(e => e!.Value).ToList();
                var extensions = values.Where(v => v.StartsWith(ExtensionKeyPrefix, StringComparison.Ordinal))
                    .Select(v => v[ExtensionKeyPrefix.Length..]).ToList();

                return s => (s.Engine != ServerEngine.Extension && engines.Contains(s.Engine))
                    || (s.Engine == ServerEngine.Extension && extensions.Contains(s.EngineExtensionId));

            case ServerFacet.Autostart:
                var off = values.Contains(AutostartOff);
                var methods = values.Select(v => Enum.TryParse<ServerAutostartMethod>(v, out var method) ? method : (ServerAutostartMethod?)null)
                    .Where(m => m != null).Select(m => m!.Value).ToList();

                return s => (off && !s.Autostart) || (s.Autostart && methods.Contains(s.AutostartMethod));

            default:
                throw new ArgumentOutOfRangeException(nameof(facet), facet, null);
        }
    }

    /// <summary>A server's <see cref="ServerFacet.Engine"/> value, e.g. "Local" or "Extension:com.example.docker".</summary>
    public static string EngineKey(ServerEngine engine, string? extensionId) =>
        engine == ServerEngine.Extension ? ExtensionKeyPrefix + extensionId : engine.ToString();

    static Expression<Func<Data.Models.Server, bool>> Within(List<Guid> ids) => s => ids.Contains(s.Id);

    static Expression<Func<Data.Models.Server, bool>> Without(List<Guid> ids) => s => !ids.Contains(s.Id);

    public static Expression<Func<Data.Models.Server, bool>> And(Expression<Func<Data.Models.Server, bool>> left, Expression<Func<Data.Models.Server, bool>> right)
    {
        var parameter = left.Parameters[0];
        var body = new ReplaceParameter(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<Data.Models.Server, bool>>(Expression.AndAlso(left.Body, body), parameter);
    }

    sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>A server filter someone named and kept, stored in the browser.</summary>
public sealed record SavedServerView(string Name, string View, Dictionary<ServerFacet, List<string>> Facets)
{
    public static SavedServerView From(string name, ServerFilters filters) =>
        new(name, filters.View, filters.Facets.Where(f => f.Value.Count > 0).ToDictionary(f => f.Key, f => f.Value.ToList()));

    public ServerFilters ToFilters() => new()
    {
        View = View,
        Facets = Facets.ToDictionary(f => f.Key, f => (IReadOnlySet<string>)f.Value.ToHashSet()),
    };
}
