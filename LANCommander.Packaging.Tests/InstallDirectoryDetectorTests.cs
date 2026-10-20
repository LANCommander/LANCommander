using LANCommander.Packaging.Changes;
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
    public void PicksTheInstallRootNotTheInstallersSourceMedia()
    {
        // Reported case: detection returned G:\Ripping\Close Combat\_CD\DATA\SOUNDS. Those were
        // the installer's own source files, recorded because copies were logged against their
        // source path instead of their destination. With destinations recorded, the common
        // ancestor is the install root.
        var paths = new[]
        {
            @"G:\Ripping\Close Combat\CC.exe",
            @"G:\Ripping\Close Combat\DATA\game.dat",
            @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\intro.wav",
            @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\battle.wav",
            @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\end.wav",
        };

        InstallDirectoryDetector.Detect(paths).ShouldBe(@"G:\Ripping\Close Combat");
    }

    [Fact]
    public void IgnoresReadWriteOpensOfTheInstallersOwnMedia()
    {
        // Reported case. The hooks derive "FILE R/W" from the access mask a caller asked for,
        // not from anything being written, so an installer that opens its source files with
        // GENERIC_READ | GENERIC_WRITE makes its own media look written to. Those paths must
        // not outvote the directory it actually wrote into.
        var changes = new[]
        {
            Change("FILE R/W", @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\intro.wav"),
            Change("FILE R/W", @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\battle.wav"),
            Change("FILE R/W", @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\end.wav"),
            Change("FILE R/W", @"G:\Ripping\Close Combat\_CD\DATA\SOUNDS\menu.wav"),
            Change("FILE WRITE", @"C:\Games\Close Combat\CC.exe"),
            Change("FILE COPY", @"C:\Games\Close Combat\DATA\game.dat"),
        };

        InstallDirectoryDetector.Detect(changes).ShouldBe(@"C:\Games\Close Combat");
    }

    [Fact]
    public void FallsBackToEverythingWhenNothingWasDemonstrablyWritten()
    {
        // An installer whose every write came through a read/write handle should still get a
        // sensible answer rather than nothing at all.
        var changes = new[]
        {
            Change("FILE R/W", @"C:\Games\Example\game.exe"),
            Change("FILE R/W", @"C:\Games\Example\Data\a.dat"),
        };

        InstallDirectoryDetector.Detect(changes).ShouldBe(@"C:\Games\Example");
    }

    [Fact]
    public void CopiesAndMovesCountAsWrites()
    {
        var changes = new[]
        {
            Change("FILE COPY", @"C:\Games\Example\game.exe"),
            Change("FILE MOVE", @"C:\Games\Example\Data\a.dat"),
            Change("FILE R/W", @"G:\Source\Media\big.bin"),
        };

        InstallDirectoryDetector.Detect(changes).ShouldBe(@"C:\Games\Example");
    }

    private static FileChange Change(string verb, string path) =>
        new() { Verb = verb, Path = path };

    [Fact]
    public void ReturnsEmptyWhenNothingWasCaptured()
    {
        InstallDirectoryDetector.Detect(Array.Empty<string>()).ShouldBe(string.Empty);
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
