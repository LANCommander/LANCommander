namespace LANCommander.Server.UI.Controls;

/// <summary>One category and its value, as the chart series consume them.</summary>
internal sealed record ChartPoint(string Category, double Value);

internal static class ChartPalette
{
    /// <summary>
    /// The Standard Dark theme's series colours (--rz-series-1 onward). Literal values, because
    /// charts are SVG and presentation attributes can't resolve CSS variables.
    /// </summary>
    public static readonly string[] Colors =
    [
        "#376df5", "#64dfdf", "#f68769", "#c161e2", "#fdd07a", "#f8629b", "#74d062", "#84a7ff",
        "#4d99f9", "#8cecec", "#fab793", "#da88ee", "#fee3ab", "#fb89c3", "#a2e389", "#b5caff",
    ];

    public static IEnumerable<string> For(int count) =>
        Enumerable.Range(0, count).Select(i => Colors[i % Colors.Length]);
}
