using System.Text.RegularExpressions;
using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class IconTests : ControlsTestContext
{
    [Fact]
    public void RendersInlineSvg_ForType()
    {
        var icon = Render<Icon>(p => p.Add(x => x.Type, IconType.Trash));

        var svg = icon.Find("svg.lc-icon");

        Assert.Equal("0 0 256 256", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.NotEmpty(svg.QuerySelectorAll("path"));
    }

    [Fact]
    public void None_RendersNothing()
    {
        var icon = Render<Icon>(p => p.Add(x => x.Type, IconType.None));

        Assert.Empty(icon.Markup.Trim());
    }

    [Fact]
    public void EveryIconType_HasGeometryInEveryWeight()
    {
        foreach (var type in Enum.GetValues<IconType>().Where(t => t != IconType.None))
        {
            foreach (var weight in Enum.GetValues<IconWeight>())
                Assert.False(string.IsNullOrEmpty(IconData.Get(type, weight)), $"{type} has no {weight} geometry");
        }
    }

    [Theory]
    [InlineData(false, false, false, "Regular")]
    [InlineData(true, false, false, "Bold")]
    [InlineData(false, false, true, "DuoTone")]
    [InlineData(false, true, false, "Fill")]
    [InlineData(true, true, true, "Fill")]
    [InlineData(true, false, true, "DuoTone")]
    public void WeightFlags_SelectGeometry_FillThenDuoToneThenBold(bool bold, bool fill, bool duoTone, string expected)
    {
        var icon = Render<Icon>(p => p
            .Add(x => x.Type, IconType.Trash)
            .Add(x => x.Bold, bold)
            .Add(x => x.Fill, fill)
            .Add(x => x.DuoTone, duoTone));

        var expectedGeometry = Regex.Matches(IconData.Get(IconType.Trash, Enum.Parse<IconWeight>(expected)), "d=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value);
        var renderedGeometry = icon.FindAll("svg path").Select(path => path.GetAttribute("d"));

        Assert.Equal(expectedGeometry, renderedGeometry);
    }

    [Fact]
    public void Spin_AddsSpinClass()
    {
        var icon = Render<Icon>(p => p.Add(x => x.Type, IconType.CircleNotch).Add(x => x.Spin, true));

        Assert.Contains("lc-icon-spin", icon.Find("svg").ClassList);
    }

    [Fact]
    public void WithoutTitle_IsHiddenFromAssistiveTech()
    {
        var svg = Render<Icon>(p => p.Add(x => x.Type, IconType.Trash)).Find("svg");

        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Title_MakesIconAnAccessibleImage()
    {
        var svg = Render<Icon>(p => p.Add(x => x.Type, IconType.Trash).Add(x => x.Title, "Delete")).Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Null(svg.GetAttribute("aria-hidden"));
        Assert.Equal("Delete", svg.QuerySelector("title")!.TextContent);
    }

    [Fact]
    public void ClassStyleAndAttributes_ReachTheSvg()
    {
        var svg = Render<Icon>(p => p
            .Add(x => x.Type, IconType.Trash)
            .Add(x => x.Class, "danger")
            .Add(x => x.Style, "font-size: 32px")
            .AddUnmatched("data-test", "icon")).Find("svg");

        Assert.Contains("lc-icon", svg.ClassList);
        Assert.Contains("danger", svg.ClassList);
        Assert.Equal("font-size: 32px", svg.GetAttribute("style"));
        Assert.Equal("icon", svg.GetAttribute("data-test"));
    }
}
