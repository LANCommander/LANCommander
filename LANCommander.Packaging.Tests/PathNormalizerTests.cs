using LANCommander.Packaging.Analysis;
using Shouldly;

namespace LANCommander.Packaging.Tests;

/// <summary>
/// The file hooks report whatever path form the caller passed in, so the same file arrives in
/// several shapes. Without normalization each shape becomes a separate change record and a
/// duplicate node in the file selection tree.
/// </summary>
public class PathNormalizerTests
{
    [Fact]
    public void StripsExtendedLengthPrefix()
    {
        PathNormalizer.Normalize(@"\\?\C:\Games\Example\game.exe")
            .ShouldBe(@"C:\Games\Example\game.exe");
    }

    [Fact]
    public void StripsExtendedLengthUncPrefix()
    {
        PathNormalizer.Normalize(@"\\?\UNC\server\share\game.exe")
            .ShouldBe(@"\\server\share\game.exe");
    }

    [Fact]
    public void ExtendedAndPlainFormsCollapseToTheSameKey()
    {
        PathNormalizer.AreSame(@"C:\Games\Example\game.exe", @"\\?\C:\Games\Example\game.exe")
            .ShouldBeTrue();
    }

    [Fact]
    public void CollapsesRelativeSegments()
    {
        PathNormalizer.Normalize(@"C:\Games\Example\..\Example\game.exe")
            .ShouldBe(@"C:\Games\Example\game.exe");
    }

    [Fact]
    public void TrimsTrailingSeparator()
    {
        PathNormalizer.Normalize(@"C:\Games\Example\").ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void KeepsSeparatorOnDriveRoot()
    {
        PathNormalizer.Normalize(@"C:\").ShouldBe(@"C:\");
    }

    [Fact]
    public void ComparisonIsCaseInsensitive()
    {
        PathNormalizer.AreSame(@"C:\Games\Example\Game.exe", @"c:\games\example\game.exe")
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInputProducesEmptyOutput(string? input)
    {
        PathNormalizer.Normalize(input).ShouldBe(string.Empty);
    }

    [Fact]
    public void UnmappedDevicePathIsLeftAlone()
    {
        // Nothing sensible can be done with a volume that has no drive letter, but it must not
        // throw and must not be mangled into something that looks valid.
        var result = PathNormalizer.Normalize(@"\Device\HarddiskVolume99\Games\game.exe");

        result.ShouldContain("Games");
    }

    [Fact]
    public void DoesNotThrowOnPathsContainingWildcards()
    {
        // Installers probe with wildcards via FindFirstFile; these cannot be canonicalized but
        // must not blow up the capture pipeline.
        Should.NotThrow(() => PathNormalizer.Normalize(@"C:\Games\Example\*.dll"));
    }
}
