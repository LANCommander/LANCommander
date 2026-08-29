using LANCommander.Packaging.Analysis;
using Shouldly;

namespace LANCommander.Packaging.Tests;

public class InstallDirectoryDetectorTests
{
    [Fact]
    public void PicksCommonAncestorRatherThanTheBusiestDirectory()
    {
        // The regression this guards: installers write most of their content into
        // subdirectories, so choosing the directory with the most files picks a subfolder
        // instead of the install root.
        var paths = new[]
        {
            @"C:\Games\Example\game.exe",
            @"C:\Games\Example\Sounds\a.wav",
            @"C:\Games\Example\Sounds\b.wav",
            @"C:\Games\Example\Sounds\c.wav",
            @"C:\Games\Example\Data\d.dat",
        };

        InstallDirectoryDetector.Detect(paths).ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void FallsBackToMostFrequentDirectoryWhenAncestorIsADriveRoot()
    {
        var paths = new[]
        {
            @"C:\Games\Example\game.exe",
            @"C:\Games\Example\data.dat",
            @"C:\Unrelated\other.txt",
        };

        // The common ancestor here is "C:", which is useless, so the busiest real directory wins.
        InstallDirectoryDetector.Detect(paths).ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void IgnoresPathsUnderIgnoredPrefixes()
    {
        var paths = new[]
        {
            @"C:\Temp\setup\extracted.tmp",
            @"C:\Games\Example\game.exe",
            @"C:\Games\Example\data.dat",
        };

        InstallDirectoryDetector.Detect(paths, [@"C:\Temp"]).ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void ReturnsEmptyWhenNothingWasCaptured()
    {
        InstallDirectoryDetector.Detect([]).ShouldBe(string.Empty);
    }

    [Fact]
    public void IgnoresRelativePaths()
    {
        InstallDirectoryDetector.Detect(["game.exe", @"data\file.dat"]).ShouldBe(string.Empty);
    }

    [Fact]
    public void SingleFileYieldsItsDirectory()
    {
        InstallDirectoryDetector.Detect([@"C:\Games\Example\game.exe"]).ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void CommonAncestorMatchesOnSegmentBoundaries()
    {
        // "Example" and "Example2" share a character prefix but not a path prefix.
        var result = InstallDirectoryDetector.FindCommonAncestor(
        [
            @"C:\Games\Example",
            @"C:\Games\Example2",
        ]);

        result.ShouldBe(@"C:\Games");
    }

    [Fact]
    public void CommonAncestorIsCaseInsensitive()
    {
        var result = InstallDirectoryDetector.FindCommonAncestor(
        [
            @"C:\Games\Example\Sounds",
            @"c:\games\example\Data",
        ]);

        result.ShouldBe(@"C:\Games\Example");
    }

    [Theory]
    [InlineData(@"C:\", true)]
    [InlineData(@"C:", true)]
    [InlineData(@"C:\Games", false)]
    public void RecognizesDriveRoots(string path, bool expected)
    {
        InstallDirectoryDetector.IsDriveRoot(path).ShouldBe(expected);
    }
}
