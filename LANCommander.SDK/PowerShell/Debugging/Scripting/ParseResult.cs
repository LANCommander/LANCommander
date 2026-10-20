#nullable enable
using System;
using System.Management.Automation.Language;

namespace LANCommander.SDK.PowerShell.Debugging.Scripting;

/// <summary>Immutable output of one parse. Safe to hand across threads.</summary>
public sealed class ParseResult
{
    public static ParseResult Empty { get; } = new(Array.Empty<Token>(), Array.Empty<ParseError>());

    public ParseResult(Token[] tokens, ParseError[] errors)
    {
        Tokens = tokens;
        Errors = errors;
    }

    public Token[] Tokens { get; }

    public ParseError[] Errors { get; }
}
