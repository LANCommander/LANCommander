using LANCommander.SDK.Services;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

public class GameInstallationFileListEntryTests
{
    private static GameInstallationFileListEntry MakeEntry(params string[] entryPaths)
    {
        var entry = new GameInstallationFileListEntry();
        entry.AddFiles(entryPaths.Select(p => new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = p,
            LocalPath = p
        }));
        return entry;
    }

    [Fact]
    public void Merge_AddsFilesFromOtherEntry()
    {
        var target = new GameInstallationFileListEntry();
        var source = MakeEntry("game.exe", "readme.txt");

        target.Merge(source);

        Assert.Equal(2, target.Files.Count);
    }

    [Fact]
    public void Merge_DoesNotAddDuplicateFiles()
    {
        var target = MakeEntry("game.exe");
        var source = MakeEntry("game.exe", "readme.txt");

        target.Merge(source);

        Assert.Equal(2, target.Files.Count);
        Assert.Single(target.Files, f => f.EntryPath == "game.exe");
    }

    [Fact]
    public void Merge_IsCaseInsensitiveForDuplicateDetection()
    {
        var target = MakeEntry("Game.exe");
        var source = MakeEntry("game.exe", "readme.txt");

        target.Merge(source);

        // "game.exe" should not be added because "Game.exe" already exists (case-insensitive)
        Assert.Equal(2, target.Files.Count);
    }

    [Fact]
    public void Merge_SetsManifestWhenTargetManifestIsNull()
    {
        var manifest = new ManifestGame { Id = Guid.NewGuid(), Title = "Test Game" };
        var target = new GameInstallationFileListEntry();
        var source = new GameInstallationFileListEntry { Manifest = manifest };

        target.Merge(source);

        Assert.Equal(manifest, target.Manifest);
    }

    [Fact]
    public void Merge_DoesNotOverwriteExistingManifest()
    {
        var originalManifest = new ManifestGame { Id = Guid.NewGuid(), Title = "Original" };
        var newManifest = new ManifestGame { Id = Guid.NewGuid(), Title = "New" };
        var target = new GameInstallationFileListEntry { Manifest = originalManifest };
        var source = new GameInstallationFileListEntry { Manifest = newManifest };

        target.Merge(source);

        Assert.Equal(originalManifest, target.Manifest);
    }

    [Fact]
    public void Merge_WithNullSource_DoesNotThrow()
    {
        var target = MakeEntry("game.exe");

        // Merge with a null-files source (empty entry)
        var emptySource = new GameInstallationFileListEntry();
        target.Merge(emptySource);

        Assert.Single(target.Files);
    }

    [Fact]
    public void AddFile_AddsFileToList()
    {
        var entry = new GameInstallationFileListEntry();
        var file = new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = "game.exe",
            LocalPath = @"C:\Games\game.exe"
        };

        entry.AddFile(file);

        Assert.Single(entry.Files);
        Assert.Equal("game.exe", entry.Files[0].EntryPath);
        Assert.Equal(@"C:\Games\game.exe", entry.Files[0].LocalPath);
    }

    [Fact]
    public void AddFiles_AddsMultipleFiles()
    {
        var entry = new GameInstallationFileListEntry();
        var files = new[]
        {
            new GameInstallationFileListEntry.FileEntry { EntryPath = "game.exe", LocalPath = @"C:\game.exe" },
            new GameInstallationFileListEntry.FileEntry { EntryPath = "data.pak", LocalPath = @"C:\data.pak" },
            new GameInstallationFileListEntry.FileEntry { EntryPath = "readme.txt", LocalPath = @"C:\readme.txt" },
        };

        entry.AddFiles(files);

        Assert.Equal(3, entry.Files.Count);
    }

    [Fact]
    public void AddFile_WithNullArgument_Throws()
    {
        var entry = new GameInstallationFileListEntry();

        Assert.Throws<ArgumentNullException>(() => entry.AddFile(null));
    }
}
