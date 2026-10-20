using System.Linq.Expressions;
using Bunit;
using LANCommander.Server.Data.Models;
using LANCommander.Server.UI.Components;
using LANCommander.Server.UI.Pages.Games.Components;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the crash-handling path: the <see cref="ErrorHandler"/> boundary
/// wrapped around the layout body, and the <see cref="DataTable{TItem}"/> data load.
///
/// The behaviour under test is the one that broke with a corrupt database: a failing table load
/// used to leave a spinner up forever and the boundary, once tripped, never cleared.
/// </summary>
[Collection("BUnit")]
public class ErrorHandlingComponentTests : BUnitTestContext
{
    public ErrorHandlingComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// A model that satisfies the DataTable's constraint but is not part of the EF model, so
    /// <c>context.Set&lt;TItem&gt;()</c> throws — a stand-in for any query the database refuses.
    /// </summary>
    public sealed class UnmappedEntity : BaseModel
    {
    }

    /// <summary>Renders content, or throws, depending on a flag the test owns.</summary>
    private sealed class ConditionalThrow : ComponentBase
    {
        [Parameter] public Func<bool> ShouldThrow { get; set; } = () => true;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (ShouldThrow())
                throw new InvalidOperationException("Simulated page failure");

            builder.AddContent(0, "Page loaded");
        }
    }

    private static RenderFragment ThrowingBody(Func<bool> shouldThrow) => builder =>
    {
        builder.OpenComponent<ConditionalThrow>(0);
        builder.AddAttribute(1, nameof(ConditionalThrow.ShouldThrow), shouldThrow);
        builder.CloseComponent();
    };

    [Fact]
    public void ErrorHandler_ShowsErrorContent_WhenBodyThrows()
    {
        var cut = Render<ErrorHandler>(parameters => parameters
            .Add(p => p.Title, "Unexpected Error")
            .Add(p => p.ChildContent, ThrowingBody(() => true)));

        Assert.Contains("Simulated page failure", cut.Markup);
        Assert.DoesNotContain("Page loaded", cut.Markup);
    }

    [Fact]
    public void ErrorHandler_Back_ReturnsToThePreviousPage()
    {
        var cut = Render<ErrorHandler>(parameters => parameters
            .Add(p => p.Title, "Unexpected Error")
            .Add(p => p.ChildContent, ThrowingBody(() => true)));

        Assert.Contains("Simulated page failure", cut.Markup);

        cut.FindAll("button")
            .First(b => b.TextContent.Contains("Back", StringComparison.OrdinalIgnoreCase))
            .Click();

        JSInterop.VerifyInvoke("history.back");
    }

    [Fact]
    public void ErrorHandler_ClearsTheError_WhenNavigatingAway()
    {
        var shouldThrow = true;

        var cut = Render<ErrorHandler>(parameters => parameters
            .Add(p => p.Title, "Unexpected Error")
            .Add(p => p.ChildContent, ThrowingBody(() => shouldThrow)));

        Assert.Contains("Simulated page failure", cut.Markup);

        shouldThrow = false;

        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.InvokeAsync(() => navigationManager.NavigateTo("/Games"));

        Assert.DoesNotContain("Simulated page failure", cut.Markup);
        Assert.Contains("Page loaded", cut.Markup);
    }

    [Fact]
    public void DataTable_ShowsLoadFailure_InsteadOfLoadingForever()
    {
        var cut = Render<DataTable<UnmappedEntity>>(parameters => parameters
            .Add(p => p.NoPaging, true)
            .Add(p => p.Query, (Expression<Func<UnmappedEntity, bool>>)(e => e.Id != Guid.Empty))
            .Add(p => p.Columns, (RenderFragment)(builder =>
            {
                builder.OpenComponent<BoundColumn<UnmappedEntity, Guid>>(0);
                builder.AddAttribute(1, nameof(BoundColumn<UnmappedEntity, Guid>.Property),
                    (Expression<Func<UnmappedEntity, Guid>>)(e => e.Id));
                builder.CloseComponent();
            })));

        // The load runs after the first render, so give it a moment to fail
        cut.WaitForAssertion(
            () => Assert.Contains("Cannot create a DbSet", cut.Markup),
            TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll(".lc-table-pending"));
        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameEditView_KeepsTheTabMenu_WhenTheContentPaneThrows()
    {
        var cut = Render<GameEditView>(parameters => parameters
            .Add(p => p.Id, Fixture.TestGameId)
            .Add(p => p.Title, "Archives")
            .Add(p => p.ChildContent, _ => ThrowingBody(() => true)));

        // The failure is contained in the content pane...
        Assert.Contains("Simulated page failure", cut.Markup);

        // ...so the tabs alongside it are still there to navigate with.
        foreach (var tab in new[] { "General", "Archives", "Actions", "Scripts" })
        {
            Assert.Contains(
                cut.FindAll(".lc-menu-item-content"),
                item => item.TextContent.Contains(tab, StringComparison.Ordinal));
        }
    }
}
