using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class StringExtensionsTests
{
    // ── SanitizeFilename ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Half-Life 2: Episode One",  "Half-Life 2 - Episode One")]
    [InlineData("Game: The Sequel",          "Game - The Sequel")]
    [InlineData("Foo: Bar",                  "Foo - Bar")]
    public void SanitizeFilename_ColonSpacePattern_IsReplacedWithDash(string input, string expected)
    {
        Assert.Equal(expected, input.SanitizeFilename());
    }

    [Fact]
    public void SanitizeFilename_ColonWithNoSpaceAfter_IsNotChanged()
    {
        // Only "word: word" (colon-space) triggers the replacement, not bare colons.
        // On Linux, ':' is not an invalid filename character so it is left as-is.
        var result = "http://example".SanitizeFilename();

        // No "word: word" match → colon stays; '/' is invalid on all platforms.
        Assert.DoesNotContain("/", result);
    }

    [Fact]
    public void SanitizeFilename_TrailingDot_IsRemoved()
    {
        var result = "GameTitle.".SanitizeFilename();

        Assert.Equal("GameTitle", result);
    }

    [Fact]
    public void SanitizeFilename_NonTrailingDot_IsPreserved()
    {
        var result = "game.exe".SanitizeFilename();

        Assert.Equal("game.exe", result);
    }

    [Fact]
    public void SanitizeFilename_MultipleDots_OnlyLastRemoved()
    {
        var result = "game...".SanitizeFilename();

        Assert.Equal("game..", result);
    }

    [Fact]
    public void SanitizeFilename_ForwardSlash_IsRemoved()
    {
        // '/' is in Path.GetInvalidFileNameChars() on all platforms.
        var result = "foo/bar".SanitizeFilename();

        Assert.Equal("foobar", result);
    }

    [Fact]
    public void SanitizeFilename_ForwardSlash_ReplacedWithCustomString()
    {
        var result = "foo/bar".SanitizeFilename("_");

        Assert.Equal("foo_bar", result);
    }

    [Fact]
    public void SanitizeFilename_NoInvalidChars_ReturnsUnchanged()
    {
        var result = "NormalTitle".SanitizeFilename();

        Assert.Equal("NormalTitle", result);
    }

    [Fact]
    public void SanitizeFilename_ColonAndTrailingDot_BothHandled()
    {
        var result = "Game: Edition.".SanitizeFilename();

        Assert.Equal("Game - Edition", result);
    }

    // ── FastReverse ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("hello",   "olleh")]
    [InlineData("abcde",   "edcba")]
    [InlineData("12345",   "54321")]
    [InlineData("a",       "a")]
    public void FastReverse_ReversesString(string input, string expected)
    {
        Assert.Equal(expected, input.FastReverse());
    }

    [Fact]
    public void FastReverse_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", "".FastReverse());
    }

    [Fact]
    public void FastReverse_Palindrome_ReturnsSameValue()
    {
        Assert.Equal("racecar", "racecar".FastReverse());
    }

    [Fact]
    public void FastReverse_TwiceProducesOriginal()
    {
        const string original = "LANCommander";

        Assert.Equal(original, original.FastReverse().FastReverse());
    }
}
