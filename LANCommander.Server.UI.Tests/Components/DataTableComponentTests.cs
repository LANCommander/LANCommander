using System.Linq.Expressions;
using Bunit;
using LANCommander.Server.Data.Models;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>The database-backed DataTable against the real server DI container and SQLite database.</summary>
[Collection("BUnit")]
public class DataTableComponentTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private sealed class GameTable : ComponentBase
    {
        [Parameter] public Expression<Func<Game, bool>>? Query { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DataTable<Game>>(0);
            builder.AddComponentParameter(1, nameof(DataTable<Game>.Query), Query);
            builder.AddComponentParameter(2, nameof(DataTable<Game>.SearchProperty), (Expression<Func<Game, string>>)(g => g.Title));
            builder.AddComponentParameter(3, nameof(DataTable<Game>.Columns), (RenderFragment)(columns =>
            {
                columns.OpenComponent<BoundColumn<Game, string>>(0);
                columns.AddComponentParameter(1, nameof(BoundColumn<Game, string>.Property), (Expression<Func<Game, string>>)(g => g.Title));
                columns.AddComponentParameter(2, nameof(BoundColumn<Game, string>.Sortable), true);
                columns.CloseComponent();

                columns.OpenComponent<BoundColumn<Game, int>>(10);
                columns.AddComponentParameter(11, nameof(BoundColumn<Game, int>.Property), (Expression<Func<Game, int>>)(g => g.Keys!.Count));
                columns.AddComponentParameter(12, nameof(BoundColumn<Game, int>.Title), "Total Keys");
                columns.AddComponentParameter(13, nameof(BoundColumn<Game, int>.Include), "Keys");
                columns.AddComponentParameter(14, nameof(BoundColumn<Game, int>.Sortable), true);
                columns.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static List<string> Titles(IRenderedComponent<GameTable> table) =>
        table.FindAll("tbody tr.rz-data-row td:first-child").Select(td => td.TextContent.Trim()).ToList();

    [Fact]
    public void LoadsRowsFromTheDatabase()
    {
        var table = Render<GameTable>();

        table.WaitForAssertion(() => Assert.Contains(BUnitServerFixture.TestGameTitle, Titles(table)), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Include_LoadsNavigationForColumn()
    {
        var table = Render<GameTable>();

        // With Keys included the count renders as 0 rather than blank from a null collection
        table.WaitForAssertion(() =>
        {
            var row = table.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains(BUnitServerFixture.TestGameTitle));
            Assert.Equal("0", row.QuerySelectorAll("td")[1].TextContent.Trim());
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Query_FiltersRows()
    {
        var table = Render<GameTable>(p => p.Add(x => x.Query, g => g.Title == "No such game"));

        table.WaitForAssertion(() => Assert.NotNull(table.Find(".lc-empty")), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Search_IgnoresCaseAndPunctuation()
    {
        var table = Render<GameTable>();
        table.WaitForAssertion(() => Assert.NotEmpty(Titles(table)), TimeSpan.FromSeconds(5));

        table.Find(".lc-table-search input").Input("test-game");

        table.WaitForAssertion(() => Assert.Equal([BUnitServerFixture.TestGameTitle], Titles(table)), TimeSpan.FromSeconds(5));
    }
}
