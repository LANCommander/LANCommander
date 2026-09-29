namespace LANCommander.Server.UI.Controls;

/// <summary>One category and its value, as the chart series consume them.</summary>
internal sealed record ChartPoint(string Category, double Value);

internal static class ChartPalette
{
    /// <summary>
    /// Series colours from the accent and status set: the fills, then their text tints. Literal
    /// values, because charts are SVG and presentation attributes can't resolve CSS variables;
    /// each mirrors the token named beside it in _theme.scss.
    /// </summary>
    public static readonly string[] Colors =
    [
        "#177DDC", // --rz-primary
        "#49AA19", // --rz-success
        "#D89614", // --rz-warning
        "#DC4446", // --rz-danger
        "#4096FF", // --lc-accent-text-color
        "#73D13D", // --lc-success-text-color
        "#FFC53D", // --lc-warning-text-color
        "#FF7875", // --lc-danger-text-color
    ];

    public static IEnumerable<string> For(int count) =>
        Enumerable.Range(0, count).Select(i => Colors[i % Colors.Length]);
}
