using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>
/// Charts size themselves through JS, which bUnit doesn't run, so these cover structure and the
/// empty state; the drawn result is covered by the Chart fixtures' visual baselines.
/// </summary>
public class ChartTests : ControlsTestContext
{
    private sealed record Point(string Name, double Seconds);

    private static readonly Point[] Points = [new("Arena Blitz", 7200), new("Neon Drift", 3600)];

    [Fact]
    public void BarChart_WithoutItems_ShowsEmptyState()
    {
        var chart = Render<BarChart<Point>>(p => p
            .Add(x => x.Items, Array.Empty<Point>())
            .Add(x => x.Label, x => x.Name)
            .Add(x => x.Value, x => x.Seconds)
            .Add(x => x.EmptyText, "No sessions yet"));

        Assert.Contains("No sessions yet", chart.Find(".lc-empty").TextContent);
        Assert.Empty(chart.FindAll(".rz-chart"));
    }

    [Fact]
    public void BarChart_WithItems_RendersChart()
    {
        var chart = Render<BarChart<Point>>(p => p
            .Add(x => x.Items, Points)
            .Add(x => x.Label, x => x.Name)
            .Add(x => x.Value, x => x.Seconds)
            .Add(x => x.Horizontal, true)
            .Add(x => x.Height, 240));

        var root = chart.Find(".rz-chart");

        Assert.Contains("height: 240px", root.GetAttribute("style"));
        Assert.Empty(chart.FindAll(".lc-empty"));
    }

    [Fact]
    public void PieChart_WithoutItems_ShowsEmptyState()
    {
        var chart = Render<PieChart<Point>>(p => p
            .Add(x => x.Items, null)
            .Add(x => x.Label, x => x.Name)
            .Add(x => x.Value, x => x.Seconds));

        Assert.NotNull(chart.Find(".lc-empty"));
    }

    [Fact]
    public void PieChart_WithItems_RendersChart()
    {
        var chart = Render<PieChart<Point>>(p => p
            .Add(x => x.Items, Points)
            .Add(x => x.Label, x => x.Name)
            .Add(x => x.Value, x => x.Seconds)
            .Add(x => x.Donut, true));

        Assert.NotNull(chart.Find(".rz-chart"));
    }
}
