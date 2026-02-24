using LANCommander.SDK.Helpers;
using System.Text.RegularExpressions;

namespace LANCommander.SDK.Tests.Helpers;

public class TextFileHelperTests : IDisposable
{
    private readonly string _tempDir;

    public TextFileHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-textfile-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteTemp(string content)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        return path;
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_tempDir, "nonexistent.txt");

        Assert.Throws<FileNotFoundException>(() =>
            TextFileHelper.ReplaceAll(path, "pattern", "replacement"));
    }

    // ── Matching and replacement ───────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_WhenPatternMatches_ReturnsUpdatedContent()
    {
        var path = WriteTemp("Hello World");

        var result = TextFileHelper.ReplaceAll(path, "World", "Earth");

        Assert.Equal("Hello Earth", result);
    }

    [Fact]
    public void ReplaceAll_WhenPatternMatches_WritesUpdatedContentToFile()
    {
        var path = WriteTemp("Hello World");

        TextFileHelper.ReplaceAll(path, "World", "Earth");

        Assert.Equal("Hello Earth", File.ReadAllText(path));
    }

    [Fact]
    public void ReplaceAll_ReplacesAllOccurrences()
    {
        var path = WriteTemp("a b a b a");

        var result = TextFileHelper.ReplaceAll(path, "a", "x");

        Assert.Equal("x b x b x", result);
    }

    // ── No-match behaviour ────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_WhenPatternDoesNotMatch_ReturnsOriginalContent()
    {
        var path = WriteTemp("Hello World");

        var result = TextFileHelper.ReplaceAll(path, "ZZZ_NOMATCH", "Replacement");

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ReplaceAll_WhenPatternDoesNotMatch_FileContentIsUnchanged()
    {
        var original = "Hello World";
        var path = WriteTemp(original);

        TextFileHelper.ReplaceAll(path, "ZZZ_NOMATCH", "Replacement");

        Assert.Equal(original, File.ReadAllText(path));
    }

    // ── Default options ───────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_DefaultOptions_IsCaseInsensitive()
    {
        var path = WriteTemp("Hello WORLD");

        var result = TextFileHelper.ReplaceAll(path, "world", "Earth");

        Assert.Equal("Hello Earth", result);
    }

    [Fact]
    public void ReplaceAll_DefaultOptions_IsMultiline()
    {
        // ^ matches start of each line in Multiline mode
        var path = WriteTemp("line1\nline2\nline3");

        var result = TextFileHelper.ReplaceAll(path, "^line", "row");

        Assert.Contains("row1", result);
        Assert.Contains("row2", result);
        Assert.Contains("row3", result);
    }

    // ── Custom options ────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_WithCaseSensitiveOptions_DoesNotMatchWrongCase()
    {
        var path = WriteTemp("Hello WORLD");

        var result = TextFileHelper.ReplaceAll(path, "world", "Earth", RegexOptions.None);

        Assert.Equal("Hello WORLD", result);
    }

    [Fact]
    public void ReplaceAll_WithCaptureGroups_SubstitutionGroupsAreExpanded()
    {
        var path = WriteTemp("2024-01-15");

        var result = TextFileHelper.ReplaceAll(path, @"(\d{4})-(\d{2})-(\d{2})", "$3/$2/$1");

        Assert.Equal("15/01/2024", result);
    }
}
