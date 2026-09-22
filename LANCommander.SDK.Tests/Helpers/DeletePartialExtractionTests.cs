using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.Helpers;

/// <summary>
/// A tool (or an addon) extracts into the game's own install directory. Cleanup after a cancelled or
/// failed extraction used to delete that directory outright, which took the whole installed game
/// with it. Only the files the extraction added may be removed.
/// </summary>
public class DeletePartialExtractionTests : IDisposable
{
    private readonly string _root;

    public DeletePartialExtractionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"lc-partial-extract-{Guid.NewGuid()}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private string WriteFile(string relativePath, string contents = "x")
    {
        var path = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return path;
    }

    [Fact]
    public void RemovesTheFilesTheExtractionCreated()
    {
        var created = new[] { WriteFile("tool.exe"), WriteFile("tool/data.bin") };

        var deleted = DirectoryHelper.DeletePartialExtraction(_root, created);

        Assert.Equal(2, deleted);
        Assert.All(created, f => Assert.False(File.Exists(f)));
    }

    /// <summary>The whole point: the game's own files and directory must survive.</summary>
    [Fact]
    public void LeavesTheDestinationAndPreExistingFilesIntact()
    {
        var gameExe = WriteFile("game.exe", "game");
        var gameData = WriteFile("Data/save.dat", "save");
        var toolFile = WriteFile("tool.exe");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { toolFile });

        Assert.True(Directory.Exists(_root));
        Assert.True(File.Exists(gameExe));
        Assert.True(File.Exists(gameData));
        Assert.False(File.Exists(toolFile));
    }

    [Fact]
    public void NeverRemovesTheDestinationEvenWhenItEndsUpEmpty()
    {
        var only = WriteFile("tool.exe");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { only });

        Assert.True(Directory.Exists(_root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void RemovesDirectoriesTheExtractionLeftEmpty()
    {
        var nested = WriteFile("tool/nested/deep/file.bin");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { nested });

        Assert.False(Directory.Exists(Path.Combine(_root, "tool")));
    }

    /// <summary>A directory that still holds the game's content is not a leftover.</summary>
    [Fact]
    public void KeepsDirectoriesThatStillHoldOtherFiles()
    {
        var gameFile = WriteFile("Shared/game.dat", "game");
        var toolFile = WriteFile("Shared/tool.dat");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { toolFile });

        Assert.True(Directory.Exists(Path.Combine(_root, "Shared")));
        Assert.True(File.Exists(gameFile));
    }

    /// <summary>
    /// A malformed archive entry must not be able to steer cleanup outside the destination.
    /// </summary>
    [Fact]
    public void IgnoresPathsOutsideTheDestination()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"lc-outside-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outside, "do not delete");

        try
        {
            var deleted = DirectoryHelper.DeletePartialExtraction(_root, new[]
            {
                outside,
                Path.Combine(_root, "..", Path.GetFileName(outside)),
            });

            Assert.Equal(0, deleted);
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void ToleratesFilesThatWereNeverWritten()
    {
        var missing = Path.Combine(_root, "never-created.bin");

        var deleted = DirectoryHelper.DeletePartialExtraction(_root, new[] { missing });

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(_root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DoesNothingWithoutADestination(string? destination)
    {
        var file = WriteFile("tool.exe");

        Assert.Equal(0, DirectoryHelper.DeletePartialExtraction(destination!, new[] { file }));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void DoesNothingWithoutAFileList()
    {
        var file = WriteFile("tool.exe");

        Assert.Equal(0, DirectoryHelper.DeletePartialExtraction(_root, null!));
        Assert.True(File.Exists(file));
    }

    // ── removeDestinationIfEmpty ──────────────────────────────────────────────

    /// <summary>
    /// A fresh base game install creates its own directory, so removing it on failure is correct and
    /// keeps a cancelled install from leaving an empty folder behind.
    /// </summary>
    [Fact]
    public void RemovesADestinationTheExtractionCreated_WhenAskedAndEmpty()
    {
        var created = WriteFile("game.exe");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { created }, removeDestinationIfEmpty: true);

        Assert.False(Directory.Exists(_root));
    }

    /// <summary>
    /// The addon case: the directory already held the base game, so it must survive even though this
    /// extraction is being rolled back.
    /// </summary>
    [Fact]
    public void KeepsADestinationThatStillHoldsContent_EvenWhenAskedToRemoveIt()
    {
        var baseGame = WriteFile("game.exe", "base game");
        var addon = WriteFile("addon.pak");

        DirectoryHelper.DeletePartialExtraction(_root, new[] { addon }, removeDestinationIfEmpty: true);

        Assert.True(Directory.Exists(_root));
        Assert.True(File.Exists(baseGame));
        Assert.False(File.Exists(addon));
    }

    [Fact]
    public void ToleratesATrailingSeparatorOnTheDestination()
    {
        var file = WriteFile("tool.exe");

        var deleted = DirectoryHelper.DeletePartialExtraction(_root + Path.DirectorySeparatorChar, new[] { file });

        Assert.Equal(1, deleted);
        Assert.True(Directory.Exists(_root));
    }
}
