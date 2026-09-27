using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Controls;

public class TableTests : ControlsTestContext
{
    public sealed record Row(Guid Id, string Title, int Players)
    {
        [Display(Name = "Max Players")]
        public int MaxPlayers => Players;
    }

    private static readonly Row[] Rows =
    [
        new(Guid.NewGuid(), "Neon Drift", 4),
        new(Guid.NewGuid(), "Arena Blitz", 16),
        new(Guid.NewGuid(), "Starfall Tactics", 8),
    ];

    /// <summary>Hosts a Table with a title column (sortable, ascending by default) and a players column.</summary>
    private sealed class Host : ComponentBase
    {
        [Parameter] public IEnumerable<Row>? Items { get; set; }

        [Parameter] public Func<TableQuery, Task<TableResult<Row>>>? Provider { get; set; }

        [Parameter] public bool Selectable { get; set; }

        [Parameter] public bool Searchable { get; set; }

        [Parameter] public int PageSize { get; set; } = 25;

        [Parameter] public EventCallback<IList<Row>> ValuesChanged { get; set; }

        [Parameter] public EventCallback<Row> OnRowDoubleClick { get; set; }

        [Parameter] public Func<Row, IEnumerable<Row>>? Children { get; set; }

        public Table<Row>? Table { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Table<Row>>(0);
            builder.AddComponentParameter(1, nameof(Table<Row>.Items), Items);
            builder.AddComponentParameter(2, nameof(Table<Row>.Provider), Provider);
            builder.AddComponentParameter(3, nameof(Table<Row>.Selectable), Selectable);
            builder.AddComponentParameter(4, nameof(Table<Row>.Searchable), Searchable);
            builder.AddComponentParameter(5, nameof(Table<Row>.SearchText), (Func<Row, string?>)(r => r.Title));
            builder.AddComponentParameter(6, nameof(Table<Row>.PageSize), PageSize);
            builder.AddComponentParameter(7, nameof(Table<Row>.ValuesChanged), ValuesChanged);
            builder.AddComponentParameter(8, nameof(Table<Row>.OnRowDoubleClick), OnRowDoubleClick);
            builder.AddComponentParameter(9, nameof(Table<Row>.Children), Children);
            builder.AddComponentParameter(10, nameof(Table<Row>.Columns), (RenderFragment)(columns =>
            {
                columns.OpenComponent<BoundColumn<Row, string>>(0);
                columns.AddComponentParameter(1, nameof(BoundColumn<Row, string>.Property), (Expression<Func<Row, string>>)(r => r.Title));
                columns.AddComponentParameter(2, nameof(BoundColumn<Row, string>.Sortable), true);
                columns.AddComponentParameter(3, nameof(BoundColumn<Row, string>.DefaultSort), ColumnSort.Ascending);
                columns.CloseComponent();

                columns.OpenComponent<BoundColumn<Row, int>>(10);
                columns.AddComponentParameter(11, nameof(BoundColumn<Row, int>.Property), (Expression<Func<Row, int>>)(r => r.MaxPlayers));
                columns.AddComponentParameter(12, nameof(BoundColumn<Row, int>.Sortable), true);
                columns.CloseComponent();

                columns.OpenComponent<ActionsColumn<Row>>(20);
                columns.AddComponentParameter(21, nameof(ActionsColumn<Row>.ChildContent), (RenderFragment<Row>)(row => b =>
                {
                    b.OpenElement(0, "button");
                    b.AddAttribute(1, "class", "edit");
                    b.AddContent(2, $"Edit {row.Title}");
                    b.CloseElement();
                }));
                columns.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(11, table => Table = (Table<Row>)table);
            builder.CloseComponent();
        }
    }

    private static List<string> Titles(IRenderedComponent<Host> host) =>
        host.FindAll("tbody tr.rz-data-row td:first-child").Select(td => td.TextContent.Trim()).ToList();

    [Fact]
    public void Renders_RowsSortedByDefaultSort()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows));

        host.WaitForAssertion(() => Assert.Equal(["Arena Blitz", "Neon Drift", "Starfall Tactics"], Titles(host)));
    }

    [Fact]
    public void Header_TakesTitleFromDisplayAttribute()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows));

        host.WaitForAssertion(() => Assert.Contains("Max Players", host.Find("thead").TextContent));
    }

    [Fact]
    public void ClickingAHeader_SortsByThatColumn()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows));
        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));

        host.FindAll("thead th").First(th => th.TextContent.Contains("Max Players")).QuerySelector(".rz-sortable-column, .rz-column-title, span")!.Click();

        host.WaitForAssertion(() => Assert.Equal(["Neon Drift", "Starfall Tactics", "Arena Blitz"], Titles(host)));
    }

    [Fact]
    public void Paging_ShowsOnePage()
    {
        var many = Enumerable.Range(1, 30).Select(i => new Row(Guid.NewGuid(), $"Game {i:D2}", i)).ToList();

        var host = Render<Host>(p => p.Add(x => x.Items, many).Add(x => x.PageSize, 25));

        host.WaitForAssertion(() => Assert.Equal(25, Titles(host).Count));
        Assert.Contains("30 rows", host.Markup);
    }

    [Fact]
    public void Provider_ReceivesPagingAndSort()
    {
        TableQuery? received = null;

        var host = Render<Host>(p => p.Add(x => x.Provider, query =>
        {
            received = query;
            return Task.FromResult(new TableResult<Row>(Rows.Take(1).ToList(), 42));
        }));

        host.WaitForAssertion(() => Assert.NotNull(received));

        Assert.Equal(0, received!.Skip);
        Assert.Equal(25, received.Take);
        Assert.Single(received.Sorts);
        Assert.False(received.Sorts[0].Descending);
    }

    [Fact]
    public void ProviderFailure_ShowsErrorWithRetry()
    {
        var attempts = 0;

        var host = Render<Host>(p => p.Add(x => x.Provider, _ =>
            ++attempts == 1
                ? throw new InvalidOperationException("Database offline")
                : Task.FromResult(new TableResult<Row>(Rows, Rows.Length))));

        host.WaitForAssertion(() => Assert.Contains("Database offline", host.Find(".lc-alert").TextContent));

        host.Find(".lc-alert button").Click();

        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));
        Assert.Empty(host.FindAll(".lc-alert"));
    }

    [Fact]
    public void Empty_ShowsEmptyState()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Array.Empty<Row>()));

        host.WaitForAssertion(() => Assert.NotNull(host.Find(".lc-empty")));
    }

    [Fact]
    public void Search_FiltersRows()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.Searchable, true));
        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));

        host.Find(".lc-table-search input").Input("arena");

        host.WaitForAssertion(() => Assert.Equal(["Arena Blitz"], Titles(host)), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Selecting_RaisesValuesChanged()
    {
        IList<Row>? selected = null;

        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.Selectable, true).Add(x => x.ValuesChanged, v => selected = v));
        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));

        host.FindAll("tbody .lc-table-select .rz-chkbox-box").First().Click();

        Assert.NotNull(selected);
        Assert.Equal("Arena Blitz", Assert.Single(selected!).Title);
    }

    [Fact]
    public void ActionsColumn_RendersPerRow()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows));

        host.WaitForAssertion(() => Assert.Equal(3, host.FindAll("button.edit").Count));
    }

    [Fact]
    public void DoubleClickingARow_RaisesOnRowDoubleClick()
    {
        Row? opened = null;

        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.OnRowDoubleClick, r => opened = r));
        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));

        host.FindAll("tbody tr.rz-data-row td").First().DoubleClick();

        Assert.Equal("Arena Blitz", opened?.Title);
    }

    [Fact]
    public void Children_MakeRowsExpandable()
    {
        var dlc = new Row(Guid.NewGuid(), "Arena Blitz: Map Pack", 16);

        var host = Render<Host>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.Children, r => r.Title == "Arena Blitz" ? [dlc] : []));

        // Radzen renders a toggle in every row and hides it where there are no children
        host.WaitForAssertion(() => Assert.Single(host.FindAll("tbody button[aria-label='Expand child item']:not([style*='hidden'])")));
    }
}
