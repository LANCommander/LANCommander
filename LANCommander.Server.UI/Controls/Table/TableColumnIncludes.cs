namespace LANCommander.Server.UI.Controls;

internal static class TableColumnIncludes
{
    public static IReadOnlyList<string> Parse(string? include) =>
        string.IsNullOrWhiteSpace(include)
            ? []
            : include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
