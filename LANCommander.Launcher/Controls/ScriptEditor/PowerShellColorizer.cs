using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Language;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>
/// Colours the editor from the PowerShell parser's own tokens.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DocumentColorizingTransformer"/> rather than an <c>IHighlighter</c>: IHighlighter
/// exists to plug a rule engine into HighlightingColorizer, with a per-line state stack, a
/// HighlightingDefinition and a colour table. We have a whole-document token array instead, so all
/// of that machinery would be scaffolding around a single method.
/// </para>
/// <para>
/// This is also why the bundled PowerShell.xshd is unused: a real parse gets here-strings,
/// sub-expressions and variables inside strings right, and a regex grammar does not.
/// </para>
/// </remarks>
public sealed class PowerShellColorizer : DocumentColorizingTransformer
{
    private readonly PowerShellTokenPalette _palette = new();
    private SyntaxSnapshot _snapshot = SyntaxSnapshot.Empty;

    /// <summary>Publish a new parse. Called on the UI thread; read on the render path.</summary>
    public void Update(SyntaxSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);

    protected override void ColorizeLine(DocumentLine line)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.Count == 0)
            return;

        int lineStart = line.Offset, lineEnd = line.EndOffset;

        for (var i = snapshot.FirstIndexAtOrBefore(lineStart); i < snapshot.Count; i++)
        {
            var token = snapshot[i];

            if (token.Extent.StartOffset >= lineEnd)
                break; // sorted, so nothing further can touch this line

            if (token.Extent.EndOffset <= lineStart)
                continue;

            Paint(token, lineStart, lineEnd);
        }
    }

    private void Paint(Token token, int lineStart, int lineEnd)
    {
        // An expandable string is ONE token covering the whole literal, so without descending into
        // its nested tokens the interpolations inside "Hello $name, you have $($items.Count)"
        // would all be flat string-coloured.
        if (token is StringExpandableToken { NestedTokens.Count: > 0 } expandable)
        {
            PaintRange(token, lineStart, lineEnd);
            foreach (var nested in expandable.NestedTokens)
                Paint(nested, lineStart, lineEnd);
            return;
        }

        PaintRange(token, lineStart, lineEnd);
    }

    private void PaintRange(Token token, int lineStart, int lineEnd)
    {
        var brush = _palette.For(token);
        if (brush is null)
            return;

        // Clamp rather than bail out. The snapshot can be a couple of hundred milliseconds stale,
        // so its offsets may run past the current document, but line.Offset/EndOffset are always
        // current. Skipping stale snapshots outright would flash the whole document to plain text
        // on every keystroke; slightly-late colours are invisible by comparison.
        var start = Math.Max(token.Extent.StartOffset, lineStart);
        var end = Math.Min(token.Extent.EndOffset, lineEnd);

        if (end <= start)
            return;

        ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
    }
}
