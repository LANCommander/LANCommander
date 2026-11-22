using LANCommander.SDK.Updating;
using Semver;

namespace LANCommander.SDK.Tests.Updating;

public class ReleaseNotesTests
{
    private static readonly ReleaseNote[] Notes =
    [
        Note("2.2.0"),
        Note("2.3.0"),
        Note("2.4.0"),
        Note("2.5.0"),
    ];

    [Fact]
    public void Load_ReadsEmbeddedNotes_NewestFirst_SkippingEmptyOnes()
    {
        var notes = ReleaseNotes.Load(typeof(ReleaseNotesTests).Assembly);

        Assert.Equal(["2.3.0", "2.2.0"], notes.Select(n => n.Version.ToString()));
        Assert.Equal("- Notes for 2.3.0", notes[0].Markdown);
    }

    [Theory]
    [InlineData("2.2.0", "2.4.0", new[] { "2.4.0", "2.3.0" })]
    [InlineData("2.2.0-rc.3", "2.4.0", new[] { "2.4.0", "2.3.0" })]
    [InlineData("2.3.0", "2.4.0-rc.1", new[] { "2.4.0" })]
    [InlineData("2.4.0-rc.1", "2.4.0-rc.2", new[] { "2.4.0" })]
    [InlineData(null, "2.4.0", new[] { "2.4.0" })]
    [InlineData("2.4.0", "2.4.1", new string[0])]
    public void Between_SelectsTheReleasesMovedPast(string? previous, string current, string[] expected)
    {
        var notes = ReleaseNotes.Between(Notes,
            previous == null ? null : SemVersion.Parse(previous, SemVersionStyles.Any),
            SemVersion.Parse(current, SemVersionStyles.Any));

        Assert.Equal(expected, notes.Select(n => n.Version.ToString()));
    }

    [Fact]
    public void ToMarkdown_HeadsEachRelease()
    {
        var markdown = ReleaseNotes.ToMarkdown([Note("2.4.0"), Note("2.3.0")]);

        Assert.Equal("## 2.4.0\n\n- 2.4.0\n\n## 2.3.0\n\n- 2.3.0", markdown.ReplaceLineEndings("\n"));
    }

    private static ReleaseNote Note(string version) => new(SemVersion.Parse(version, SemVersionStyles.Any), $"- {version}");
}
