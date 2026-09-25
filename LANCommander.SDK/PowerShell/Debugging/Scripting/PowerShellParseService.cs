#nullable enable
using System;
using System.Management.Automation.Language;

namespace LANCommander.SDK.PowerShell.Debugging.Scripting;

/// <summary>
/// Wraps the PowerShell parser for syntax highlighting and error reporting. Pure and thread-safe:
/// callable from a worker thread.
/// </summary>
public static class PowerShellParseService
{
    /// <summary>
    /// Tokenize and parse. Uses the overload without a file name; the file name only populates
    /// <c>Extent.File</c>, which highlighting does not need.
    /// </summary>
    public static ParseResult Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return ParseResult.Empty;

        try
        {
            Parser.ParseInput(text, out var tokens, out var errors);
            return new ParseResult(tokens ?? Array.Empty<Token>(), errors ?? Array.Empty<ParseError>());
        }
        catch (Exception)
        {
            // The parser is meant to be total, but a highlighting pass must never take down the editor.
            return ParseResult.Empty;
        }
    }
}
