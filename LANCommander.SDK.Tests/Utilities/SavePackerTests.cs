using LANCommander.SDK.Enums;
using LANCommander.SDK.Utilities;
using System.IO.Compression;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;

namespace LANCommander.SDK.Tests.Utilities;

public class SavePackerTests : IDisposable
{
    private readonly string _tempDir;

    public SavePackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-savepacker-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // Creates a SavePath whose resolved paths are relative to _tempDir.
    // workingDirectory defaults to "{InstallDir}" so that ExpandEnvironmentVariables
    // resolves it to _tempDir, making Path.Combine(_tempDir, path) the effective target.
    private ManifestSavePath MakeSavePath(
        SavePathType type,
        string path,
        string workingDirectory = "{InstallDir}",
        bool isRegex = false) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Path = path,
        WorkingDirectory = workingDirectory,
        IsRegex = isRegex
    };

    private void WriteFile(string relativePath, string content = "data")
    {
        var full = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    // Open the packed zip and return all entry names normalised to forward slashes.
    private static List<string> EntryKeys(Stream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        return zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void HasEntries_OnNewPacker_ReturnsFalse()
    {
        using var packer = new SavePacker(_tempDir);

        Assert.False(packer.HasEntries());
    }

    [Fact]
    public void HasManifest_OnNewPacker_ReturnsFalse()
    {
        using var packer = new SavePacker(_tempDir);

        Assert.False(packer.HasManifest());
    }

    // ── AddManifestAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddManifestAsync_SetsHasManifestTrue()
    {
        using var packer = new SavePacker(_tempDir);

        await packer.AddManifestAsync(new ManifestGame { Id = Guid.NewGuid(), Title = "Test" });

        Assert.True(packer.HasManifest());
    }

    [Fact]
    public async Task AddManifestAsync_SetsHasEntriesTrue()
    {
        using var packer = new SavePacker(_tempDir);

        await packer.AddManifestAsync(new ManifestGame { Id = Guid.NewGuid(), Title = "Test" });

        Assert.True(packer.HasEntries());
    }

    [Fact]
    public async Task AddManifestAsync_PackedArchiveContainsManifestYml()
    {
        using var packer = new SavePacker(_tempDir);
        await packer.AddManifestAsync(new ManifestGame { Id = Guid.NewGuid(), Title = "Test" });

        var stream = await packer.PackAsync();
        var keys = EntryKeys(stream);

        Assert.Contains(keys, k => string.Equals(k, "Manifest.yml", StringComparison.OrdinalIgnoreCase));
    }

    // ── PackAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PackAsync_ReturnsStreamAtPositionZero()
    {
        using var packer = new SavePacker(_tempDir);

        var stream = await packer.PackAsync();

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task PackAsync_StreamCanBeOpenedAsZip()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        var stream = await packer.PackAsync();

        var ex = Record.Exception(() =>
        {
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            _ = zip.Entries.Count;
        });
        Assert.Null(ex);
    }

    // ── Fluent interface ──────────────────────────────────────────────────────

    [Fact]
    public void AddPath_ReturnsSamePacker()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");

        var result = packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        Assert.Same(packer, result);
    }

    [Fact]
    public void AddPaths_ReturnsSamePacker()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");

        var result = packer.AddPaths([MakeSavePath(SavePathType.File, "save.dat")]);

        Assert.Same(packer, result);
    }

    // ── Argument / type validation ────────────────────────────────────────────

    [Fact]
    public void AddPath_WithUnknownSavePathType_ThrowsArgumentOutOfRangeException()
    {
        using var packer = new SavePacker(_tempDir);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            packer.AddPath(MakeSavePath((SavePathType)99, "irrelevant")));
    }

    // ── AddRegistryPath guard ─────────────────────────────────────────────────

    [Fact]
    public void AddRegistryPath_WithNonRegistryType_AddsNoEntries()
    {
        using var packer = new SavePacker(_tempDir);
        var filePath = MakeSavePath(SavePathType.File, "save.dat");

        packer.AddRegistryPath(filePath);

        Assert.False(packer.HasEntries());
    }

    // ── AddPath: single file ──────────────────────────────────────────────────

    [Fact]
    public void AddPath_WithExistingFile_SetsHasEntriesTrue()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");

        packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        Assert.True(packer.HasEntries());
    }

    [Fact]
    public async Task AddPath_WithExistingFile_EntryKeyStartsWithFilesPrefix()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.All(keys, k => Assert.StartsWith("Files/", k));
    }

    [Fact]
    public async Task AddPath_WithExistingFile_EntryKeyContainsSavePathId()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        var savePath = MakeSavePath(SavePathType.File, "save.dat");
        packer.AddPath(savePath);

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => k.Contains(savePath.Id.ToString()));
    }

    [Fact]
    public async Task AddPath_WithExistingFile_EntryKeyEndsWithFilename()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => k.EndsWith("save.dat"));
    }

    [Fact]
    public void AddPath_WithNonExistentPath_AddsNoEntries()
    {
        using var packer = new SavePacker(_tempDir);
        // "missing.dat" is never created on disk
        packer.AddPath(MakeSavePath(SavePathType.File, "missing.dat"));

        Assert.False(packer.HasEntries());
    }

    // ── AddPath: directory ────────────────────────────────────────────────────

    [Fact]
    public async Task AddPath_WithDirectory_AddsAllContainedFiles()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("saves/slot1.dat");
        WriteFile("saves/slot2.dat");
        WriteFile("saves/slot3.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "saves"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public async Task AddPath_WithDirectory_EntryKeysContainEachFilename()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("saves/slot1.dat");
        WriteFile("saves/slot2.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "saves"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => k.EndsWith("slot1.dat"));
        Assert.Contains(keys, k => k.EndsWith("slot2.dat"));
    }

    [Fact]
    public async Task AddPath_WithDirectory_RecursivelyAddsNestedFiles()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("saves/slot1.dat");
        WriteFile("saves/sub/slot2.dat");
        packer.AddPath(MakeSavePath(SavePathType.File, "saves"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, k => k.EndsWith("slot2.dat"));
    }

    // ── AddPath: regex pattern ────────────────────────────────────────────────

    [Fact]
    public async Task AddPath_WithRegexPattern_AddsOnlyMatchingFiles()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save1.dat");
        WriteFile("save2.dat");
        WriteFile("config.ini");
        packer.AddPath(MakeSavePath(SavePathType.File, @"\.dat$", isRegex: true));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.EndsWith(".dat", k));
    }

    [Fact]
    public async Task AddPath_WithRegexPattern_ExcludesNonMatchingFiles()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        WriteFile("config.ini");
        packer.AddPath(MakeSavePath(SavePathType.File, @"\.dat$", isRegex: true));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.DoesNotContain(keys, k => k.EndsWith(".ini"));
    }

    // ── AddPaths ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddPaths_WithEmptyCollection_AddsNoEntries()
    {
        using var packer = new SavePacker(_tempDir);

        packer.AddPaths([]);

        Assert.False(packer.HasEntries());
    }

    [Fact]
    public async Task AddPaths_WithMultiplePaths_AddsFilesFromEach()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        WriteFile("config.ini");
        packer.AddPaths([
            MakeSavePath(SavePathType.File, "save.dat"),
            MakeSavePath(SavePathType.File, "config.ini")
        ]);

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, k => k.EndsWith("save.dat"));
        Assert.Contains(keys, k => k.EndsWith("config.ini"));
    }

    [Fact]
    public async Task AddPaths_TwoDifferentSavePathIds_ProducesEntriesUnderDistinctPrefixes()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("slot1.dat");
        WriteFile("slot2.dat");
        var path1 = MakeSavePath(SavePathType.File, "slot1.dat");
        var path2 = MakeSavePath(SavePathType.File, "slot2.dat");
        packer.AddPaths([path1, path2]);

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => k.Contains(path1.Id.ToString()));
        Assert.Contains(keys, k => k.Contains(path2.Id.ToString()));
    }

    // ── Manifest + files together ─────────────────────────────────────────────

    [Fact]
    public async Task PackAsync_WithManifestAndFile_ContainsBothEntries()
    {
        using var packer = new SavePacker(_tempDir);
        WriteFile("save.dat");
        await packer.AddManifestAsync(new ManifestGame { Id = Guid.NewGuid(), Title = "Test" });
        packer.AddPath(MakeSavePath(SavePathType.File, "save.dat"));

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => string.Equals(k, "Manifest.yml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(keys, k => k.EndsWith("save.dat"));
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var packer = new SavePacker(_tempDir);

        var ex = Record.Exception(() => packer.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void Using_Block_DisposesWithoutThrowing()
    {
        var ex = Record.Exception(() =>
        {
            using var packer = new SavePacker(_tempDir);
        });

        Assert.Null(ex);
    }
}
