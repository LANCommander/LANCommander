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

    /// <summary>
    /// Hosts a Table with a title column (sortable, ascending by default), a players column, a
    /// "Seats" column hidden by default, and an actions column.
    /// </summary>
    private sealed class Host : ComponentBase
    {
        [Parameter] public IEnumerable<Row>? Items { get; set; }

        [Parameter] public Func<TableQuery, Task<TableResult<Row>>>? Provider { get; set; }

        [Parameter] public bool Selectable { get; set; }

        [Parameter] public bool Searchable { get; set; }

        /// <summary>Left unset, the table's own default applies.</summary>
        [Parameter] public int? PageSize { get; set; }

        [Parameter] public EventCallback<IList<Row>> ValuesChanged { get; set; }

        [Parameter] public EventCallback<Row> OnRowDoubleClick { get; set; }

        [Parameter] public Func<Row, IEnumerable<Row>>? Children { get; set; }

        [Parameter] public bool ColumnPicker { get; set; }

        [Parameter] public bool ShowTotal { get; set; }

        [Parameter] public bool ShowSortSummary { get; set; }

        /// <summary>Puts the expand toggle in the title column.</summary>
        [Parameter] public bool TitleExpander { get; set; }

        [Parameter] public Func<IReadOnlyList<Row>, string?>? ChildSummary { get; set; }

        [Parameter] public Func<Row, string?>? RowMark { get; set; }

        public Table<Row>? Table { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Table<Row>>(0);
            builder.AddComponentParameter(1, nameof(Table<Row>.Items), Items);
            builder.AddComponentParameter(2, nameof(Table<Row>.Provider), Provider);
            builder.AddComponentParameter(3, nameof(Table<Row>.Selectable), Selectable);
            builder.AddComponentParameter(4, nameof(Table<Row>.Searchable), Searchable);
            builder.AddComponentParameter(5, nameof(Table<Row>.SearchText), (Func<Row, string?>)(r => r.Title));

            if (PageSize != null)
                builder.AddComponentParameter(6, nameof(Table<Row>.PageSize), PageSize.Value);

            builder.AddComponentParameter(7, nameof(Table<Row>.ValuesChanged), ValuesChanged);
            builder.AddComponentParameter(8, nameof(Table<Row>.OnRowDoubleClick), OnRowDoubleClick);
            builder.AddComponentParameter(9, nameof(Table<Row>.Children), Children);
            builder.AddComponentParameter(12, nameof(Table<Row>.ColumnPicker), ColumnPicker);
            builder.AddComponentParameter(13, nameof(Table<Row>.ShowTotal), ShowTotal);
            builder.AddComponentParameter(14, nameof(Table<Row>.ShowSortSummary), ShowSortSummary);
            builder.AddComponentParameter(15, nameof(Table<Row>.ChildSummary), ChildSummary);
            builder.AddComponentParameter(16, nameof(Table<Row>.RowMark), RowMark);
            builder.AddComponentParameter(10, nameof(Table<Row>.Columns), (RenderFragment)(columns =>
            {
                columns.OpenComponent<BoundColumn<Row, string>>(0);
                columns.AddComponentParameter(1, nameof(BoundColumn<Row, string>.Property), (Expression<Func<Row, string>>)(r => r.Title));
                columns.AddComponentParameter(2, nameof(BoundColumn<Row, string>.Sortable), true);
                columns.AddComponentParameter(3, nameof(BoundColumn<Row, string>.DefaultSort), ColumnSort.Ascending);
                columns.AddComponentParameter(4, nameof(BoundColumn<Row, string>.Expander), TitleExpander);
                columns.CloseComponent();

                columns.OpenComponent<BoundColumn<Row, int>>(10);
                columns.AddComponentParameter(11, nameof(BoundColumn<Row, int>.Property), (Expression<Func<Row, int>>)(r => r.MaxPlayers));
                columns.AddComponentParameter(12, nameof(BoundColumn<Row, int>.Sortable), true);
                columns.CloseComponent();

                columns.OpenComponent<BoundColumn<Row, int>>(15);
                columns.AddComponentParameter(16, nameof(BoundColumn<Row, int>.Property), (Expression<Func<Row, int>>)(r => r.Players));
                columns.AddComponentParameter(17, nameof(BoundColumn<Row, int>.Title), "Seats");
                columns.AddComponentParameter(18, nameof(BoundColumn<Row, int>.Hide), true);
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

    private static List<Row> Many(int count) =>
        Enumerable.Range(1, count).Select(i => new Row(Guid.NewGuid(), $"Game {i:D2}", i)).ToList();

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
        var host = Render<Host>(p => p.Add(x => x.Items, Many(30)).Add(x => x.PageSize, 25));

        host.WaitForAssertion(() => Assert.Equal(25, Titles(host).Count));
        Assert.Equal("1–25 of 30", host.Find(".lc-table-range").TextContent.Trim());
    }

    [Fact]
    public void PageSize_DefaultsToFifty()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Many(60)));

        host.WaitForAssertion(() => Assert.Equal(50, Titles(host).Count));
        Assert.Equal("1–50 of 60", host.Find(".lc-table-range").TextContent.Trim());
    }

    [Fact]
    public void Footer_NumbersThePages_AndGoesToTheOneClicked()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Many(30)).Add(x => x.PageSize, 10));
        host.WaitForAssertion(() => Assert.Equal(10, Titles(host).Count));

        var pages = host.FindAll(".lc-table-pages .lc-table-page:not(.lc-table-page-step)");
        Assert.Equal(["1", "2", "3"], pages.Select(p => p.TextContent.Trim()));
        Assert.Equal("page", pages[0].GetAttribute("aria-current"));
        Assert.True(host.Find("button[aria-label='Previous page']").HasAttribute("disabled"));

        host.Find("button[aria-label='Page 2']").Click();

        host.WaitForAssertion(() => Assert.Equal("Game 11", Titles(host)[0]));
        Assert.Equal("11–20 of 30", host.Find(".lc-table-range").TextContent.Trim());
        Assert.Equal("page", host.Find("button[aria-label='Page 2']").GetAttribute("aria-current"));

        host.Find("button[aria-label='Next page']").Click();

        host.WaitForAssertion(() => Assert.Equal("Game 21", Titles(host)[0]));
        Assert.True(host.Find("button[aria-label='Next page']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(0, 5, new[] { 0, 1, 2, 3, 4 })]
    [InlineData(0, 20, new[] { 0, 1, 2, 3, 4, -1, 19 })]
    [InlineData(10, 20, new[] { 0, -1, 9, 10, 11, -1, 19 })]
    [InlineData(18, 20, new[] { 0, -1, 15, 16, 17, 18, 19 })]
    public void PageWindow_KeepsFirstLastAndNeighbours(int page, int count, int[] expected)
    {
        Assert.Equal(expected, Table<Row>.PageWindow(page, count));
    }

    [Fact]
    public void Toolbar_ShowsTotalAndSort()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.ShowTotal, true).Add(x => x.ShowSortSummary, true));

        host.WaitForAssertion(() => Assert.Equal("3 results", host.Find(".lc-table-total").TextContent.Trim()));
        Assert.Contains("Title", host.Find(".lc-table-sorted-value").TextContent);
    }

    [Fact]
    public void ColumnPicker_ShowsAHiddenColumn()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.ColumnPicker, true));
        host.WaitForAssertion(() => Assert.Equal(3, Titles(host).Count));

        // Hidden by default, so not in the header until picked
        Assert.DoesNotContain("Seats", host.Find("thead").TextContent);

        host.Find(".lc-table-columns .lc-button").Click();

        var seats = host.WaitForElement(".lc-table-column-picker .lc-table-column-option:nth-child(3) .rz-chkbox-box");
        Assert.Contains("Seats", host.Find(".lc-table-column-picker").TextContent);

        seats.Click();

        host.WaitForAssertion(() => Assert.Contains("Seats", host.Find("thead").TextContent));
    }

    [Fact]
    public void RowMark_IsRenderedOnTheRow()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.RowMark, r => r.Title == "Neon Drift" ? "missing-art" : null));

        host.WaitForAssertion(() => Assert.Single(host.FindAll("tbody tr[data-lc-mark='missing-art']")));
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
        Assert.Equal(50, received.Take);
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
    public void Search_ShowsTheShortcutKey()
    {
        var host = Render<Host>(p => p.Add(x => x.Items, Rows).Add(x => x.Searchable, true));

        Assert.Equal("/", host.Find(".lc-table-search-box .lc-table-search-key").TextContent.Trim());
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

    [Fact]
    public void TitleExpander_TogglesChildRows_WithTheirSummary()
    {
        var dlc = new Row(Guid.NewGuid(), "Arena Blitz: Map Pack", 16);

        var host = Render<Host>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.Children, r => r.Title == "Arena Blitz" ? [dlc] : [])
            .Add(x => x.TitleExpander, true)
            .Add(x => x.ChildSummary, children => $"{children.Count} expansion"));

        var expander = host.WaitForElement(".lc-table-expander");
        Assert.Single(host.FindAll(".lc-table-expander"));
        Assert.Equal("1 expansion", host.Find(".lc-table-child-summary").TextContent.Trim());
        Assert.Contains("lc-table-inline-expander", host.Find(".lc-table").ClassName);

        expander.Click();

        host.WaitForAssertion(() =>
        {
            var child = host.Find("tbody tr[data-lc-child]");
            Assert.Contains("Arena Blitz: Map Pack", child.TextContent);
            Assert.NotNull(child.QuerySelector(".lc-table-expandable-child"));
        });
        Assert.Equal("true", host.Find(".lc-table-expander").GetAttribute("aria-expanded"));

        host.Find(".lc-table-expander").Click();

        host.WaitForAssertion(() => Assert.Empty(host.FindAll("tbody tr[data-lc-child]")));
    }
}
