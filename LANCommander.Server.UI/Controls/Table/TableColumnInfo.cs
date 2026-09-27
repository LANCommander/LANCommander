using System.Linq.Expressions;

namespace LANCommander.Server.UI.Controls;

/// <summary>What a column tells its table: how to identify, sort and load it.</summary>
internal sealed class TableColumnInfo
{
    /// <summary>Stable identifier used for sorting and remembering visibility.</summary>
    public required string Key { get; init; }

    public string? Title { get; set; }

    /// <summary>The value rows are ordered by when sorting on this column, if it is sortable.</summary>
    public LambdaExpression? SortExpression { get; set; }

    /// <summary>Navigation properties the query must load for this column to render.</summary>
    public IReadOnlyList<string> Includes { get; set; } = [];
}

/// <summary>What columns can see of the table they are in.</summary>
internal interface ITableHost
{
    void Register(TableColumnInfo column);

    void Unregister(TableColumnInfo column);

    bool IsVisible(TableColumnInfo column, bool hiddenByDefault);

    Radzen.SortOrder? InitialSortOrder(TableColumnInfo column, ColumnSort defaultSort);

    bool Small { get; }
}
