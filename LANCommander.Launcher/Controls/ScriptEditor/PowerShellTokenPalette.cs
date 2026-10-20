using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Language;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>Maps PowerShell tokens to brushes.</summary>
public sealed class PowerShellTokenPalette
{
    // Immutable brushes: these are read on every render pass for every visible token, and
    // immutable ones skip the change-tracking machinery.
    private static readonly IBrush Comment = new ImmutableSolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
    private static readonly IBrush String = new ImmutableSolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
    private static readonly IBrush Variable = new ImmutableSolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
    private static readonly IBrush Parameter = new ImmutableSolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
    private static readonly IBrush Number = new ImmutableSolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));
    private static readonly IBrush Command = new ImmutableSolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
    private static readonly IBrush Keyword = new ImmutableSolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
    private static readonly IBrush Member = new ImmutableSolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
    private static readonly IBrush TypeName = new ImmutableSolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
    private static readonly IBrush Operator = new ImmutableSolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));

    /// <summary>
    /// Null means "leave the default foreground alone".
    /// </summary>
    /// <remarks>
    /// Order matters. Kinds that are unambiguous are tested first, then flags, because a token can
    /// be of kind <c>Identifier</c> while carrying the <c>CommandName</c> flag: testing the kind
    /// first would paint every command as plain text.
    /// </remarks>
    public IBrush? For(Token token)
    {
        switch (token.Kind)
        {
            case TokenKind.Comment:
                return Comment;

            case TokenKind.StringLiteral:
            case TokenKind.StringExpandable:
            case TokenKind.HereStringLiteral:
            case TokenKind.HereStringExpandable:
                return String;

            case TokenKind.Variable:
            case TokenKind.SplattedVariable:
                return Variable;

            case TokenKind.Parameter:
                return Parameter;

            case TokenKind.Number:
                return Number;
        }

        var flags = token.TokenFlags;

        if (flags.HasFlag(TokenFlags.CommandName))
            return Command;

        if (flags.HasFlag(TokenFlags.Keyword))
            return Keyword;

        if (flags.HasFlag(TokenFlags.MemberName))
            return Member;

        if (flags.HasFlag(TokenFlags.TypeName))
            return TypeName;

        if (flags.HasFlag(TokenFlags.BinaryOperator) ||
            flags.HasFlag(TokenFlags.UnaryOperator) ||
            flags.HasFlag(TokenFlags.AssignmentOperator))
            return Operator;

        return null;
    }
}
