using XtermBlazor;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LANCommander.Server.UI.Components;

/// <summary>What a console line is, which decides its colour.</summary>
public enum ConsoleLineKind
{
    Output,
    Host,
    Error,
    Warning,
    Verbose,
    Debug,
    Information,
    Prompt,
    Echo,
    System,
}

/// <summary>
/// The launcher's script debugger console colours (LANCommander.Launcher
/// ViewModels/ScriptDebugger/Items/ConsoleLineViewModel.cs), so a script's output reads the same in
/// both. Lines are coloured, never badged; only warning and error rows are tinted. Literal values,
/// because xterm paints to a canvas and can't read CSS variables. The launcher's translucent whites
/// are resolved against the console well (#141414, --lc-well) since a terminal has no alpha.
/// </summary>
public static class ConsolePalette
{
    public const string Well = "#141414";            // --lc-well

    public const string Output = "#D9D9D9";          // --lc-text-title-color
    public const string Host = "#FFFFFF";
    public const string Error = "#FF7875";           // --lc-danger-text-color
    public const string Warning = "#FFC53D";         // --lc-warning-text-color
    public const string Verbose = "#9CDCFE";
    public const string Debug = "#6BC8D6";
    public const string Information = "#9CDCFE";
    public const string Prompt = "#DCDCAA";
    public const string Echo = "#959595";            // #8CFFFFFF over the well
    public const string System = "#4096FF";          // --lc-accent-text-color

    // Row tints: the status fill at 10% over the well
    public const string WarningRow = "#282114";      // --rz-warning #D89614
    public const string ErrorRow = "#281919";        // --rz-danger #DC4446

    // ANSI's magenta and cyan, for output that colours itself
    const string AnsiMagenta = "#9A8CD6";
    const string AnsiCyan = "#6BC8D6";

    public static readonly Theme Theme = new()
    {
        Background = Well,
        Foreground = Output,
        Cursor = Prompt,                             // the block caret after the prompt
        CursorAccent = Well,
        SelectionBackground = "rgba(64, 150, 255, 0.3)", // accent text at 30%
        Black = Well,
        Red = Error,
        Green = "#73D13D",                           // --lc-success-text-color
        Yellow = Warning,
        Blue = System,
        Magenta = AnsiMagenta,
        Cyan = AnsiCyan,
        White = Output,
        BrightBlack = Echo,
        BrightRed = Error,
        BrightGreen = "#73D13D",
        BrightYellow = Warning,
        BrightBlue = Information,
        BrightMagenta = AnsiMagenta,
        BrightCyan = AnsiCyan,
        BrightWhite = Host,
    };

    /// <summary>Terminal options every console shares: the palette and the 12px mono face.</summary>
    public static TerminalOptions CreateOptions(CursorStyle cursorStyle = CursorStyle.Bar) => new()
    {
        CursorBlink = true,
        CursorStyle = cursorStyle,
        FontFamily = "\"Roboto Mono\", ui-monospace, Consolas, monospace",
        FontSize = 12,
        Theme = Theme,
    };

    /// <summary>19px lines on 12px text, as xterm's multiple; <see cref="Terminal"/> applies it.</summary>
    public const double LineHeight = 19.0 / 12.0;

    public static ConsoleLineKind KindOf(LogLevel level) => level switch
    {
        LogLevel.Critical or LogLevel.Error => ConsoleLineKind.Error,
        LogLevel.Warning => ConsoleLineKind.Warning,
        LogLevel.Debug => ConsoleLineKind.Debug,
        LogLevel.Trace => ConsoleLineKind.Verbose,
        _ => ConsoleLineKind.Output,
    };

    public static string ColorOf(ConsoleLineKind kind) => kind switch
    {
        ConsoleLineKind.Host => Host,
        ConsoleLineKind.Error => Error,
        ConsoleLineKind.Warning => Warning,
        ConsoleLineKind.Verbose => Verbose,
        ConsoleLineKind.Debug => Debug,
        ConsoleLineKind.Information => Information,
        ConsoleLineKind.Prompt => Prompt,
        ConsoleLineKind.Echo => Echo,
        ConsoleLineKind.System => System,
        _ => Output,
    };

    static string? RowOf(ConsoleLineKind kind) => kind switch
    {
        ConsoleLineKind.Error => ErrorRow,
        ConsoleLineKind.Warning => WarningRow,
        _ => null,
    };

    /// <summary>
    /// <paramref name="text"/> as terminal lines in the kind's colour. Warning and error lines also
    /// fill their row, to the right edge, with the tint.
    /// </summary>
    public static IEnumerable<string> Format(string text, ConsoleLineKind kind)
    {
        var foreground = Sgr(38, ColorOf(kind));
        var row = RowOf(kind) is { } tint ? Sgr(48, tint) : "";
        var clearToEnd = row.Length > 0 ? "\x1b[K" : "";

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            yield return $"\x1b[0m{row}{foreground}{line}{clearToEnd}\x1b[0m";
    }

    /// <summary>A truecolor SGR sequence: 38 for the foreground, 48 for the background.</summary>
    static string Sgr(int target, string hex)
    {
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);

        return $"\x1b[{target};2;{r};{g};{b}m";
    }
}
