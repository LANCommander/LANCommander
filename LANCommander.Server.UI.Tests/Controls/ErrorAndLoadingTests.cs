using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Controls;

public class ErrorAndLoadingTests : ControlsTestContext
{
    private sealed class Thrower : ComponentBase
    {
        public static bool ShouldThrow { get; set; } = true;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Kaboom");

            builder.AddContent(0, "recovered");
        }
    }

    private static RenderFragment ThrowerContent => builder =>
    {
        builder.OpenComponent<Thrower>(0);
        builder.CloseComponent();
    };

    [Fact]
    public void ErrorHandler_ShowsErrorPage_AndRecoversOnRetry()
    {
        Thrower.ShouldThrow = true;

        var handler = Render<ErrorHandler>(p => p.Add(x => x.ChildContent, ThrowerContent));

        Assert.Contains("Kaboom", handler.Find(".lc-error-page").TextContent);

        Thrower.ShouldThrow = false;
        handler.FindAll("button").Single(b => b.TextContent.Contains("Retry")).Click();

        Assert.Contains("recovered", handler.Markup);
        Assert.Empty(handler.FindAll(".lc-error-page"));
    }

    [Fact]
    public void ErrorHandler_Inline_ShowsCompactAlert()
    {
        Thrower.ShouldThrow = true;

        var handler = Render<ErrorHandler>(p => p.Add(x => x.ChildContent, ThrowerContent).Add(x => x.Inline, true));

        Assert.Contains("Kaboom", handler.Find(".lc-error-inline").TextContent);
        Assert.Empty(handler.FindAll(".lc-error-page"));
    }

    [Fact]
    public async Task AsyncContent_ShowsSpinnerThenContent()
    {
        var load = new TaskCompletionSource<string>();

        var content = Render<AsyncContent<string>>(p => p
            .Add(x => x.Load, () => load.Task)
            .Add(x => x.ChildContent, value => $"<p class='value'>{value}</p>"));

        Assert.NotEmpty(content.FindAll(".lc-spin"));

        load.SetResult("Arena Blitz");
        await Task.Yield();

        content.WaitForAssertion(() => Assert.Equal("Arena Blitz", content.Find("p.value").TextContent));
    }

    [Fact]
    public void AsyncContent_Failure_ShowsErrorAndRetries()
    {
        var attempts = 0;

        var content = Render<AsyncContent<string>>(p => p
            .Add(x => x.Load, () => ++attempts == 1 ? throw new InvalidOperationException("Database offline") : Task.FromResult("ok"))
            .Add(x => x.ChildContent, value => $"<p class='value'>{value}</p>"));

        content.WaitForAssertion(() => Assert.Contains("Database offline", content.Find(".lc-alert").TextContent));

        content.Find(".lc-alert button").Click();

        content.WaitForAssertion(() => Assert.Equal("ok", content.Find("p.value").TextContent));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void AsyncContent_EmptyCollection_ShowsEmptyTemplate()
    {
        var content = Render<AsyncContent<List<string>>>(p => p
            .Add(x => x.Load, () => Task.FromResult(new List<string>()))
            .Add(x => x.ChildContent, _ => "<p class='value'>items</p>")
            .Add(x => x.Empty, "<p class='empty'>nothing</p>"));

        content.WaitForAssertion(() => Assert.NotNull(content.Find("p.empty")));
        Assert.Empty(content.FindAll("p.value"));
    }

    [Fact]
    public void AsyncContent_ReloadsWhenKeyChanges()
    {
        var loads = 0;

        var content = Render<AsyncContent<int>>(p => p
            .Add(x => x.Load, () => Task.FromResult(++loads))
            .Add(x => x.Key, "a")
            .Add(x => x.ChildContent, value => $"<p class='value'>{value}</p>"));

        content.WaitForAssertion(() => Assert.Equal("1", content.Find("p.value").TextContent));

        content.Render(p => p.Add(x => x.Key, "a"));
        Assert.Equal(1, loads);

        content.Render(p => p.Add(x => x.Key, "b"));
        content.WaitForAssertion(() => Assert.Equal("2", content.Find("p.value").TextContent));
    }

    [Fact]
    public void PageHeader_RendersTitleSubtitleAndExtra()
    {
        var header = Render<PageHeader>(p => p
            .Add(x => x.Title, "Games")
            .Add(x => x.Subtitle, "8")
            .Add(x => x.Extra, "<button>Add Game</button>"));

        Assert.Equal("Games", header.Find("h1").TextContent.Trim());
        Assert.Equal("8", header.Find(".lc-page-header-subtitle").TextContent);
        Assert.NotNull(header.Find(".lc-page-header-extra button"));
    }

    [Fact]
    public void PageContent_ContainsErrorsInline()
    {
        Thrower.ShouldThrow = true;

        var content = Render<PageContent>(p => p.Add(x => x.ChildContent, ThrowerContent));

        Assert.NotNull(content.Find(".lc-page-content .lc-error-inline"));
    }
}
