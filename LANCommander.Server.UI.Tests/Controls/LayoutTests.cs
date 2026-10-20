using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class LayoutTests : ControlsTestContext
{
    [Fact]
    public void Flex_DefaultsToHorizontalWithoutGap()
    {
        var div = Render<Flex>(p => p.AddChildContent("x")).Find("div");

        Assert.Contains("lc-flex", div.ClassList);
        Assert.DoesNotContain("lc-flex-vertical", div.ClassList);
        Assert.Contains("lc-gap-none", div.ClassList);
    }

    [Fact]
    public void Flex_MapsFlagsAndEnums()
    {
        var div = Render<Flex>(p => p
            .Add(x => x.Vertical, true)
            .Add(x => x.Wrap, true)
            .Add(x => x.Gap, Gap.Small)
            .Add(x => x.Align, Align.Center)
            .Add(x => x.Justify, Justify.SpaceBetween)
            .Add(x => x.Class, "custom")).Find("div");

        Assert.Equal(
            ["lc-flex", "lc-flex-vertical", "lc-flex-wrap", "lc-gap-small", "lc-align-center", "lc-justify-space-between", "custom"],
            div.ClassList);
    }

    [Fact]
    public void Flex_Inline_UsesInlineFlex()
    {
        var div = Render<Flex>(p => p.Add(x => x.Inline, true)).Find("div");

        Assert.Contains("lc-flex-inline", div.ClassList);
        Assert.DoesNotContain("lc-flex", div.ClassList);
    }

    [Fact]
    public void Row_SetsGutterVariablesAndWrap()
    {
        var div = Render<Row>(p => p
            .Add(x => x.Gap, Gap.Medium)
            .Add(x => x.VerticalGap, Gap.Small)
            .Add(x => x.NoWrap, true)
            .Add(x => x.Justify, Justify.End)
            .Add(x => x.Style, "margin-top: 16px")).Find("div");

        Assert.Contains("lc-row-nowrap", div.ClassList);
        Assert.Contains("lc-justify-end", div.ClassList);
        Assert.Equal("--lc-gutter: 16px; --lc-row-gap: 8px; margin-top: 16px", div.GetAttribute("style"));
    }

    [Fact]
    public void Column_MapsSpansPerBreakpoint()
    {
        var div = Render<Column>(p => p.Add(x => x.Span, 8).Add(x => x.Xs, 24).Add(x => x.Md, 12)).Find("div");

        Assert.Equal(["lc-col", "lc-col-8", "lc-col-xs-24", "lc-col-md-12"], div.ClassList);
    }

    [Fact]
    public void Column_ClampsSpanAndSupportsFill()
    {
        var div = Render<Column>(p => p.Add(x => x.Span, 30).Add(x => x.Fill, true)).Find("div");

        Assert.Contains("lc-col-24", div.ClassList);
        Assert.Equal("flex: 1 1 auto", div.GetAttribute("style"));
    }
}
