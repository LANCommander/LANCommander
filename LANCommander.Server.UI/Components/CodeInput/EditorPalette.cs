using BlazorMonaco.Editor;
using Microsoft.JSInterop;

namespace LANCommander.Server.UI.Components;

/// <summary>
/// The code editor's colours. Token colours are the launcher's script editor palette
/// (LANCommander.Launcher Controls/ScriptEditor/PowerShellTokenPalette.cs), verbatim, so both
/// editors paint PowerShell identically; the chrome comes from the token set. Literal values,
/// because Monaco themes are data, not CSS.
/// </summary>
public static class EditorPalette
{
    public const string ThemeName = "lancommander-dark";

    static readonly StandaloneThemeData Theme = new()
    {
        Base = "vs-dark",
        Inherit = true,
        Rules =
        [
            new TokenThemeRule { Token = "comment", Foreground = "6A9955" },
            new TokenThemeRule { Token = "string", Foreground = "CE9178" },
            new TokenThemeRule { Token = "variable", Foreground = "9CDCFE" },
            new TokenThemeRule { Token = "attribute.name", Foreground = "B0B0B0" }, // parameters
            new TokenThemeRule { Token = "number", Foreground = "B5CEA8" },
            new TokenThemeRule { Token = "predefined", Foreground = "DCDCAA" },     // commands
            new TokenThemeRule { Token = "keyword", Foreground = "569CD6" },
            new TokenThemeRule { Token = "identifier", Foreground = "DCDCDC" },     // members
            new TokenThemeRule { Token = "type", Foreground = "4EC9B0" },
            new TokenThemeRule { Token = "delimiter", Foreground = "D4D4D4" },      // operators
            new TokenThemeRule { Token = "operator", Foreground = "D4D4D4" },
        ],
        Colors = new Dictionary<string, string>
        {
            ["editor.background"] = "#141414",                    // --lc-well
            ["editor.foreground"] = "#D4D4D4",                    // the operator colour
            ["editorGutter.background"] = "#0F0F0F",              // a step below the well; its hairline is in CSS
            ["editorLineNumber.foreground"] = "#5C5C5C",
            ["editorLineNumber.activeForeground"] = "#D9D9D9",    // text
            ["editor.lineHighlightBackground"] = "#FFFFFF0D",     // white at 5%; the accent edge is in CSS
            ["editor.selectionBackground"] = "#4096FF4D",         // accent text at 30%
            ["editorCursor.foreground"] = "#4096FF",              // accent text
            ["editorWidget.background"] = "#303030",              // popover
            ["editorWidget.border"] = "#FFFFFF1A",                // hairline
            ["editorSuggestWidget.background"] = "#303030",
            ["editorSuggestWidget.border"] = "#FFFFFF1F",         // overlay hairline, 12%
            ["editorSuggestWidget.selectedBackground"] = "#177DDC33", // primary at 20%
            ["editorHoverWidget.background"] = "#303030",
            ["editorHoverWidget.border"] = "#FFFFFF1A",
            ["editorError.foreground"] = "#FF7875",               // danger text
            ["editorWarning.foreground"] = "#FFC53D",             // warning text
            ["scrollbarSlider.background"] = "#FFFFFF1A",
            ["scrollbarSlider.hoverBackground"] = "#FFFFFF24",
            ["scrollbarSlider.activeBackground"] = "#FFFFFF33",
        },
    };

    /// <summary>Defines the theme and makes it current. Themes are global to the page, so every editor shares it.</summary>
    public static async Task ApplyAsync(IJSRuntime js)
    {
        await Global.DefineTheme(js, ThemeName, Theme);
        await Global.SetTheme(js, ThemeName);
    }
}
