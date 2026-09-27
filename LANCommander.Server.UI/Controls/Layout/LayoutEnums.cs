namespace LANCommander.Server.UI.Controls;

/// <summary>Space between the children of a <see cref="Flex"/> or <see cref="Row"/>.</summary>
public enum Gap
{
    None,
    /// <summary>8px</summary>
    Small,
    /// <summary>16px</summary>
    Medium,
    /// <summary>24px</summary>
    Large,
}

/// <summary>Cross-axis alignment of children.</summary>
public enum Align
{
    Stretch,
    Start,
    Center,
    End,
    Baseline,
}

/// <summary>Main-axis distribution of children.</summary>
public enum Justify
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

internal static class LayoutClasses
{
    public static string For(Gap gap) => gap switch
    {
        Gap.Small => "lc-gap-small",
        Gap.Medium => "lc-gap-medium",
        Gap.Large => "lc-gap-large",
        _ => "lc-gap-none",
    };

    public static string For(Align align) => align switch
    {
        Align.Start => "lc-align-start",
        Align.Center => "lc-align-center",
        Align.End => "lc-align-end",
        Align.Baseline => "lc-align-baseline",
        _ => "lc-align-stretch",
    };

    public static string For(Justify justify) => justify switch
    {
        Justify.Center => "lc-justify-center",
        Justify.End => "lc-justify-end",
        Justify.SpaceBetween => "lc-justify-space-between",
        Justify.SpaceAround => "lc-justify-space-around",
        Justify.SpaceEvenly => "lc-justify-space-evenly",
        _ => "lc-justify-start",
    };
}
