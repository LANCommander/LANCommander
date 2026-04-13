using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using LANCommander.SDK.Tests.Fixtures;
using LANCommander.SDK.Utilities;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;

namespace LANCommander.SDK.Tests.Saves;

/// <summary>
/// Tests the save upload + packing cycle.
///
/// <c>SaveClient.UploadAsync</c> ends with an HTTP POST; only the packing half is
/// exercised here.  <c>SaveClient.PackAsync</c> (no HTTP) and <c>SavePacker</c>
/// directly are both tested so we cover the path the launcher takes after a game exits.
/// </summary>
public class SaveUploadTests : IDisposable
{
    private readonly string _tempDir;

    // Only the packing / file-system methods are exercised;
    // API-calling members are never invoked, so all DI deps can be null.
    private readonly SaveClient _saveClient = new(null, null, null, null);

    public SaveUploadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-save-upload-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── SaveClient.PackAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task PackAsync_ProducesReadableZipStream()
    {
        using var fixture = new FakeGameFixture();

        var stream = await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest);

        var ex = Record.Exception(() =>
        {
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            _ = zip.Entries.Count;
        });
        Assert.Null(ex);
    }

    [Fact]
    public async Task PackAsync_IncludesManifestYml()
    {
        using var fixture = new FakeGameFixture();

        var keys = EntryKeys(await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest));

        Assert.Contains(keys, k => string.Equals(k, "Manifest.yml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PackAsync_IncludesSaveSlotFiles()
    {
        using var fixture = new FakeGameFixture();

        var keys = EntryKeys(await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest));

        Assert.Contains(keys, k => k.EndsWith("slot1.sav"));
        Assert.Contains(keys, k => k.EndsWith("slot2.sav"));
    }

    [Fact]
    public async Task PackAsync_IncludesConfigFile()
    {
        using var fixture = new FakeGameFixture();

        var keys = EntryKeys(await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest));

        Assert.Contains(keys, k => k.EndsWith("config.cfg"));
    }

    [Fact]
    public async Task PackAsync_WithNoSavePaths_ProducesArchiveWithOnlyManifest()
    {
        var installDir = Path.Combine(_tempDir, "empty-game");
        Directory.CreateDirectory(installDir);

        var manifest = new ManifestGame
        {
            Id        = Guid.NewGuid(),
            Title     = "No-Save Game",
            SavePaths = new List<ManifestSavePath>()
        };

        var keys = EntryKeys(await _saveClient.PackAsync(installDir, manifest));

        Assert.Single(keys);
        Assert.Contains(keys, k => string.Equals(k, "Manifest.yml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PackAsync_WithRegexSavePath_ExcludesNonMatchingFiles()
    {
        var installDir = CreateGameDir(
            ("save1.sav", "SAVE1"),
            ("save2.sav", "SAVE2"),
            ("readme.txt", "README"));

        var manifest = new ManifestGame
        {
            Id    = Guid.NewGuid(),
            Title = "Regex Game",
            SavePaths = new List<ManifestSavePath>
            {
                new()
                {
                    Id               = Guid.NewGuid(),
                    Type             = SavePathType.File,
                    Path             = @"\.sav$",
                    WorkingDirectory = "{InstallDir}",
                    IsRegex          = true
                }
            }
        };

        var keys = EntryKeys(await _saveClient.PackAsync(installDir, manifest));

        Assert.DoesNotContain(keys, k => k.EndsWith(".txt"));
        Assert.Contains(keys, k => k.EndsWith(".sav"));
    }

    [Fact]
    public async Task PackAsync_WithMultipleSavePaths_AllFilesIncluded()
    {
        using var fixture = new FakeGameFixture();

        var keys = EntryKeys(await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest));

        Assert.Contains(keys, k => k.Contains(fixture.SavesDirSavePath.Id.ToString()));
        Assert.Contains(keys, k => k.Contains(fixture.ConfigFileSavePath.Id.ToString()));
    }

    [Fact]
    public async Task PackAsync_EachSavePath_StoredUnderDistinctIdPrefix()
    {
        using var fixture = new FakeGameFixture();

        var keys = EntryKeys(await _saveClient.PackAsync(fixture.InstallDirectory, fixture.Manifest));

        var saveDirKeys = keys.Where(k => k.Contains(fixture.SavesDirSavePath.Id.ToString())).ToList();
        var configKeys  = keys.Where(k => k.Contains(fixture.ConfigFileSavePath.Id.ToString())).ToList();

        Assert.NotEmpty(saveDirKeys);
        Assert.NotEmpty(configKeys);
        Assert.Empty(saveDirKeys.Intersect(configKeys));
    }

    // ── SavePacker directly ───────────────────────────────────────────────────

    [Fact]
    public async Task SavePacker_WithFakeGame_ProducesExpectedFileCount()
    {
        using var fixture = new FakeGameFixture();
        using var packer  = new SavePacker(fixture.InstallDirectory);

        packer.AddPaths(fixture.Manifest.SavePaths);
        await packer.AddManifestAsync(fixture.Manifest);

        // slot1.sav + slot2.sav + config.cfg + Manifest.yml = 4
        Assert.Equal(4, EntryKeys(await packer.PackAsync()).Count);
    }

    [Fact]
    public async Task SavePacker_PackedContent_MatchesOriginalFileContent()
    {
        using var fixture = new FakeGameFixture();
        using var packer  = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.ConfigFileSavePath);

        var stream = await packer.PackAsync();
        stream.Position = 0;

        using var zip         = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var       configEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("config.cfg"));

        Assert.NotNull(configEntry);

        using var reader  = new StreamReader(configEntry.Open());
        var       content = await reader.ReadToEndAsync();

        Assert.Equal(FakeGameFixture.ConfigContent, content);
    }

    [Fact]
    public async Task SavePacker_WithDirectorySavePath_PacksAllFilesInDirectory()
    {
        using var fixture = new FakeGameFixture();
        using var packer  = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.SavesDirSavePath);

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, k => k.EndsWith("slot1.sav"));
        Assert.Contains(keys, k => k.EndsWith("slot2.sav"));
    }

    [Fact]
    public async Task SavePacker_AfterGameExit_NewSaveFileIsIncluded()
    {
        using var fixture = new FakeGameFixture();

        // Simulate a new save written after game launch
        File.WriteAllText(Path.Combine(fixture.SavesDirectory, "slot3.sav"), "NEW_SLOT");

        using var packer = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.SavesDirSavePath);

        var keys = EntryKeys(await packer.PackAsync());

        Assert.Contains(keys, k => k.EndsWith("slot3.sav"));
    }

    [Fact]
    public async Task SavePacker_AfterGameExit_ModifiedSaveFileContentIsPreserved()
    {
        using var fixture = new FakeGameFixture();

        // Simulate the game overwriting slot1 with new data
        File.WriteAllText(fixture.SaveFileSlot1Path, "UPDATED_SAVE");

        using var packer = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.SavesDirSavePath);

        var stream = await packer.PackAsync();
        stream.Position = 0;

        using var zip   = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var       entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("slot1.sav"));

        Assert.NotNull(entry);

        using var reader  = new StreamReader(entry.Open());
        var       content = await reader.ReadToEndAsync();

        Assert.Equal("UPDATED_SAVE", content);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string CreateGameDir(params (string relativePath, string content)[] files)
    {
        var dir = Path.Combine(_tempDir, $"game-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        foreach (var (relativePath, content) in files)
        {
            var full = Path.Combine(dir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return dir;
    }

    private static List<string> EntryKeys(Stream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        return zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
    }
}
