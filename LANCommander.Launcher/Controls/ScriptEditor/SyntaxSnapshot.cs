using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Language;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>
/// An immutable parse result laid out for fast per-line lookup.
/// </summary>
/// <remarks>
/// <para>
/// <c>ColorizeLine</c> is called once per visible line on every render pass, while the token array
/// covers the whole document and can hold tens of thousands of entries. Scanning it linearly per
/// line is O(visible x tokens) per frame, which is visibly janky on a large script. The parallel
/// array of start offsets turns that into a binary search plus a short forward walk.
/// </para>
/// <para>
/// Built off the UI thread and published by atomic reference swap, so it is never mutated in place.
/// </para>
/// </remarks>
public sealed class SyntaxSnapshot
{
    public static SyntaxSnapshot Empty { get; } =
        new(Array.Empty<Token>(), Array.Empty<ParseError>());

    private readonly Token[] _tokens;
    private readonly int[] _starts;

    public SyntaxSnapshot(Token[] tokens, ParseError[] errors)
    {
        _tokens = tokens;
        Errors = errors;

        _starts = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
            _starts[i] = tokens[i].Extent.StartOffset;

#if DEBUG
        // Parser.ParseInput documents tokens in ascending start order and the binary search below
        // depends on it. Assert rather than assume, and rather than paying for a sort.
        for (var i = 1; i < _starts.Length; i++)
        {
            System.Diagnostics.Debug.Assert(
                _starts[i] >= _starts[i - 1],
                "Tokens are not in ascending start order; SyntaxSnapshot lookup would be wrong.");
        }
#endif
    }

    public ParseError[] Errors { get; }

    public int Count => _tokens.Length;

    public Token this[int index] => _tokens[index];

    /// <summary>
    /// Index of the first token that could overlap <paramref name="offset"/>. One token before the
    /// match, because a multi-line token (a here-string, a block comment) can start well above the
    /// line being painted.
    /// </summary>
    public int FirstIndexAtOrBefore(int offset)
    {
        if (_starts.Length == 0)
            return 0;

        var index = Array.BinarySearch(_starts, offset);
        if (index < 0)
            index = ~index - 1;

        return Math.Max(0, index);
    }
}
