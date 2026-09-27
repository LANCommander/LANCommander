using System.Linq.Expressions;
using System.Reflection;

namespace LANCommander.Server.UI.Controls;

public enum ColumnSort
{
    None,
    Ascending,
    Descending,
}

/// <summary>One column the rows are ordered by.</summary>
/// <param name="Expression">The column's value, e.g. <c>g =&gt; g.Title</c>.</param>
public sealed record TableSort(LambdaExpression Expression, bool Descending);

/// <summary>The page of rows a <see cref="Table{TItem}"/> needs from its provider.</summary>
public sealed record TableQuery(int Skip, int Take, IReadOnlyList<TableSort> Sorts, string? Search, IReadOnlyList<string> Includes)
{
    /// <summary>Applies <see cref="Sorts"/> and paging to <paramref name="source"/>.</summary>
    public IQueryable<TItem> Apply<TItem>(IQueryable<TItem> source, bool page = true)
    {
        source = TableSorting.Apply(source, Sorts);

        return page ? source.Skip(Skip).Take(Take) : source;
    }
}

/// <summary>A page of rows, the total across all pages, and optionally each row's children.</summary>
public sealed record TableResult<TItem>(IReadOnlyList<TItem> Items, int Total)
{
    /// <summary>Child rows shown beneath a row when it is expanded, e.g. a game's DLC.</summary>
    public IReadOnlyDictionary<TItem, IReadOnlyList<TItem>>? Children { get; init; }
}

internal static class TableSorting
{
    private static readonly MethodInfo OrderByMethod = QueryableMethod(nameof(Queryable.OrderBy));
    private static readonly MethodInfo OrderByDescendingMethod = QueryableMethod(nameof(Queryable.OrderByDescending));
    private static readonly MethodInfo ThenByMethod = QueryableMethod(nameof(Queryable.ThenBy));
    private static readonly MethodInfo ThenByDescendingMethod = QueryableMethod(nameof(Queryable.ThenByDescending));

    public static IQueryable<TItem> Apply<TItem>(IQueryable<TItem> source, IReadOnlyList<TableSort> sorts)
    {
        for (var i = 0; i < sorts.Count; i++)
        {
            var sort = sorts[i];

            var method = (i == 0, sort.Descending) switch
            {
                (true, false) => OrderByMethod,
                (true, true) => OrderByDescendingMethod,
                (false, false) => ThenByMethod,
                (false, true) => ThenByDescendingMethod,
            };

            source = (IQueryable<TItem>)method
                .MakeGenericMethod(typeof(TItem), sort.Expression.ReturnType)
                .Invoke(null, [source, sort.Expression])!;
        }

        return source;
    }

    private static MethodInfo QueryableMethod(string name) =>
        typeof(Queryable).GetMethods().Single(m => m.Name == name && m.GetParameters().Length == 2);
}
