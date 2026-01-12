namespace LANCommander.UI.Components;

/// <summary>
/// Represents a CSS cursor value.
/// </summary>
public sealed record Cursor(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Factory for standard CSS cursor values.
/// </summary>
public static class Cursors
{
    public static Cursor Auto => new("auto");
    public static Cursor Default => new("default");
    public static Cursor None => new("none");

    public static Cursor ContextMenu => new("context-menu");
    public static Cursor Help => new("help");
    public static Cursor Pointer => new("pointer");
    public static Cursor Progress => new("progress");
    public static Cursor Wait => new("wait");

    public static Cursor Cell => new("cell");
    public static Cursor Crosshair => new("crosshair");
    public static Cursor Text => new("text");
    public static Cursor VerticalText => new("vertical-text");

    public static Cursor Alias => new("alias");
    public static Cursor Copy => new("copy");
    public static Cursor Move => new("move");
    public static Cursor NoDrop => new("no-drop");
    public static Cursor NotAllowed => new("not-allowed");

    public static Cursor Grab => new("grab");
    public static Cursor Grabbing => new("grabbing");

    public static Cursor AllScroll => new("all-scroll");
    public static Cursor ColumnResize => new("col-resize");
    public static Cursor RowResize => new("row-resize");

    public static Cursor NorthResize => new("n-resize");
    public static Cursor EastResize => new("e-resize");
    public static Cursor SouthResize => new("s-resize");
    public static Cursor WestResize => new("w-resize");

    public static Cursor NorthEastResize => new("ne-resize");
    public static Cursor NorthWestResize => new("nw-resize");
    public static Cursor SouthEastResize => new("se-resize");
    public static Cursor SouthWestResize => new("sw-resize");

    public static Cursor EastWestResize => new("ew-resize");
    public static Cursor NorthSouthResize => new("ns-resize");
    public static Cursor NorthEastSouthWestResize => new("nesw-resize");
    public static Cursor NorthWestSouthEastResize => new("nwse-resize");

    public static Cursor ZoomIn => new("zoom-in");
    public static Cursor ZoomOut => new("zoom-out");
}