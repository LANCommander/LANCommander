using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class FeedbackTests : ControlsTestContext
{
    [Theory]
    [InlineData(false, false, "rz-info")]
    [InlineData(true, false, "rz-success")]
    [InlineData(false, true, "rz-danger")]
    public void Alert_StatusMapsToStyle(bool success, bool danger, string expectedClass)
    {
        var alert = Render<Alert>(p => p.AddChildContent("message").Add(x => x.Success, success).Add(x => x.Danger, danger));

        var root = alert.Find(".lc-alert");

        Assert.Contains(expectedClass, root.ClassList);
    }

    [Fact]
    public void Alert_ShowsTitleIconAndContent()
    {
        var alert = Render<Alert>(p => p.Add(x => x.Warning, true).Add(x => x.Title, "Heads up").AddChildContent("details"));

        Assert.Equal("Heads up", alert.Find(".lc-alert-title").TextContent);
        Assert.Contains("details", alert.Find(".lc-alert-content").TextContent);
        Assert.Single(alert.FindAll("svg.lc-alert-icon"));
    }

    [Fact]
    public void Alert_NoIcon_HidesIcon()
    {
        var alert = Render<Alert>(p => p.AddChildContent("message").Add(x => x.NoIcon, true));

        Assert.Empty(alert.FindAll("svg.lc-alert-icon"));
    }

    [Fact]
    public void Spin_Standalone_ShowsIndicator()
    {
        var spin = Render<Spin>(p => p.Add(x => x.Tip, "Checking"));

        Assert.Equal("Checking", spin.Find(".lc-spin-tip").TextContent);
        Assert.Equal("status", spin.Find(".lc-spin").GetAttribute("role"));
    }

    [Fact]
    public void Spin_Standalone_NotLoading_RendersNothing()
    {
        var spin = Render<Spin>(p => p.Add(x => x.Loading, false));

        Assert.Empty(spin.Markup.Trim());
    }

    [Fact]
    public void Spin_WithContent_OverlaysOnlyWhileLoading()
    {
        var spin = Render<Spin>(p => p.AddChildContent("<p>content</p>").Add(x => x.Loading, true));

        Assert.Equal("true", spin.Find(".lc-spin-container").GetAttribute("aria-busy"));
        Assert.Single(spin.FindAll(".lc-spin-overlay"));
        Assert.NotNull(spin.Find("p"));

        spin.Render(p => p.Add(x => x.Loading, false));

        Assert.Empty(spin.FindAll(".lc-spin-overlay"));
        Assert.Equal("false", spin.Find(".lc-spin-container").GetAttribute("aria-busy"));
    }

    [Fact]
    public void Empty_DefaultsDescription()
    {
        var empty = Render<Empty>();

        Assert.Equal("No data", empty.Find(".lc-empty-description").TextContent.Trim());
    }

    [Fact]
    public void Empty_CustomDescriptionAndActions()
    {
        var empty = Render<Empty>(p => p
            .Add(x => x.Description, "No games yet")
            .AddChildContent("<button>Add game</button>")
            .Add(x => x.Small, true));

        Assert.Equal("No games yet", empty.Find(".lc-empty-description").TextContent.Trim());
        Assert.NotNull(empty.Find(".lc-empty-actions button"));
        Assert.Contains("lc-empty-small", empty.Find(".lc-empty").ClassList);
    }

    [Fact]
    public void Result_RendersStatusTitlesAndActions()
    {
        var result = Render<Result>(p => p
            .Add(x => x.Danger, true)
            .Add(x => x.Title, "Import failed")
            .Add(x => x.SubTitle, "The archive is corrupt")
            .AddChildContent("<button>Retry</button>"));

        Assert.Contains("lc-result-danger", result.Find(".lc-result").ClassList);
        Assert.Equal("Import failed", result.Find(".lc-result-title").TextContent);
        Assert.Equal("The archive is corrupt", result.Find(".lc-result-subtitle").TextContent);
        Assert.NotNull(result.Find(".lc-result-actions button"));
    }
}
