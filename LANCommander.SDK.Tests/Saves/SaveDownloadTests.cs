using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using LANCommander.SDK.Tests.Fixtures;
using LANCommander.SDK.Utilities;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;

namespace LANCommander.SDK.Tests.Saves;

/// <summary>
/// Tests the save download + extraction cycle.
///
/// HTTP calls are not exercised.  Instead, the save archive is pre-built on-disk
/// using <see cref="SavePacker"/> and restored with the same file-movement logic
/// that <c>SaveClient.DownloadAsync</c> uses, giving us full coverage of the
/// extraction flow without needing a running server.
/// </summary>
public class SaveDownloadTests : IDisposable
{
    private readonly string _tempDir;

    // Only the filesystem / pure-computation methods are exercised here;
    // API-calling members are never invoked, so all DI deps can be null.
    private readonly SaveClient _saveClient = new(null, null, null, null);

    public SaveDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-save-download-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── FakeGameFixture – structure sanity checks ─────────────────────────────

    [Fact]
    public void FakeGame_ManifestWrittenToDisk()
    {
        using var fixture = new FakeGameFixture();

        Assert.True(ManifestHelper.Exists(fixture.InstallDirectory, fixture.GameId));
    }

    [Fact]
    public void FakeGame_SaveFilesExistOnDisk()
    {
        using var fixture = new FakeGameFixture();

        Assert.True(File.Exists(fixture.SaveFileSlot1Path));
        Assert.True(File.Exists(fixture.SaveFileSlot2Path));
        Assert.True(File.Exists(fixture.ConfigFilePath));
    }

    [Fact]
    public void FakeGame_ManifestContainsTwoKeys()
    {
        using var fixture = new FakeGameFixture();

        Assert.Equal(2, fixture.Manifest.Keys.Count);
    }

    [Fact]
    public void FakeGame_ManifestContainsExpectedScriptTypes()
    {
        using var fixture = new FakeGameFixture();
        var types = fixture.Manifest.Scripts.Select(s => s.Type).ToList();

        Assert.Contains(ScriptType.Install,    types);
        Assert.Contains(ScriptType.Uninstall,  types);
        Assert.Contains(ScriptType.BeforeStart,types);
        Assert.Contains(ScriptType.AfterStop,  types);
        Assert.Contains(ScriptType.NameChange, types);
        Assert.Contains(ScriptType.KeyChange,  types);
    }

    [Fact]
    public void FakeGame_ManifestContainsThreeMediaItems()
    {
        using var fixture = new FakeGameFixture();

        Assert.Equal(3, fixture.Manifest.Media.Count);
    }

    [Fact]
    public void FakeGame_ManifestContainsCoverIconAndBackground()
    {
        using var fixture = new FakeGameFixture();
        var types = fixture.Manifest.Media.Select(m => m.Type).ToList();

        Assert.Contains(MediaType.Cover,      types);
        Assert.Contains(MediaType.Icon,       types);
        Assert.Contains(MediaType.Background, types);
    }

    [Fact]
    public void FakeGame_ScriptFilesExistOnDisk()
    {
        using var fixture = new FakeGameFixture();

        var scriptTypes = new[]
        {
            ScriptType.Install, ScriptType.Uninstall,
            ScriptType.BeforeStart, ScriptType.AfterStop,
            ScriptType.NameChange, ScriptType.KeyChange
        };

        foreach (var type in scriptTypes)
        {
            var path = ScriptHelper.GetScriptFilePath(fixture.InstallDirectory, fixture.GameId, type);
            Assert.True(File.Exists(path), $"Script file missing for {type}: {path}");
        }
    }

    // ── Packed-archive structure ──────────────────────────────────────────────

    [Fact]
    public async Task PackedSave_ContainsManifestYml()
    {
        using var fixture = new FakeGameFixture();

        var stream = await BuildArchiveStreamAsync(fixture.InstallDirectory, fixture.Manifest);
        var keys = EntryKeys(stream);

        Assert.Contains(keys, k => string.Equals(k, "Manifest.yml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PackedSave_SavesDirEntries_PrefixedWithCorrectSavePathId()
    {
        using var fixture = new FakeGameFixture();
        using var packer  = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.SavesDirSavePath);

        var keys           = EntryKeys(await packer.PackAsync());
        var expectedPrefix = $"Files/{fixture.SavesDirSavePath.Id}/";

        Assert.All(keys, k => Assert.StartsWith(expectedPrefix, k));
    }

    [Fact]
    public async Task PackedSave_ConfigFileEntry_PrefixedWithCorrectSavePathId()
    {
        using var fixture = new FakeGameFixture();
        using var packer  = new SavePacker(fixture.InstallDirectory);
        packer.AddPath(fixture.ConfigFileSavePath);

        var keys           = EntryKeys(await packer.PackAsync());
        var expectedPrefix = $"Files/{fixture.ConfigFileSavePath.Id}/";

        Assert.All(keys, k => Assert.StartsWith(expectedPrefix, k));
    }

    [Fact]
    public async Task PackedSave_AllSaveFiles_ArePresent()
    {
        using var fixture = new FakeGameFixture();

        var stream = await BuildArchiveStreamAsync(fixture.InstallDirectory, fixture.Manifest);
        var keys   = EntryKeys(stream);

        Assert.Contains(keys, k => k.EndsWith("slot1.sav"));
        Assert.Contains(keys, k => k.EndsWith("slot2.sav"));
        Assert.Contains(keys, k => k.EndsWith("config.cfg"));
    }

    // ── Round-trip: pack → write to disk → extract → restore ─────────────────

    [Fact]
    public async Task RoundTrip_SavesDirFiles_LandInDestInstallDirectory()
    {
        using var fixture = new FakeGameFixture();

        var archivePath = await WriteSaveArchiveAsync(fixture.InstallDirectory, fixture.Manifest);
        var destDir     = CreateDestDir();

        RestoreSaveArchive(archivePath, destDir, fixture.Manifest);

        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot1.sav")));
        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot2.sav")));
    }

    [Fact]
    public async Task RoundTrip_ConfigFile_LandsInDestInstallDirectory()
    {
        using var fixture = new FakeGameFixture();

        var archivePath = await WriteSaveArchiveAsync(fixture.InstallDirectory, fixture.Manifest);
        var destDir     = CreateDestDir();

        RestoreSaveArchive(archivePath, destDir, fixture.Manifest);

        Assert.True(File.Exists(Path.Combine(destDir, "config.cfg")));
    }

    [Fact]
    public async Task RoundTrip_SaveContents_ArePreserved()
    {
        using var fixture = new FakeGameFixture();

        var archivePath = await WriteSaveArchiveAsync(fixture.InstallDirectory, fixture.Manifest);
        var destDir     = CreateDestDir();

        RestoreSaveArchive(archivePath, destDir, fixture.Manifest);

        Assert.Equal(FakeGameFixture.Slot1Content,  File.ReadAllText(Path.Combine(destDir, "saves", "slot1.sav")));
        Assert.Equal(FakeGameFixture.Slot2Content,  File.ReadAllText(Path.Combine(destDir, "saves", "slot2.sav")));
        Assert.Equal(FakeGameFixture.ConfigContent, File.ReadAllText(Path.Combine(destDir, "config.cfg")));
    }

    [Fact]
    public async Task RoundTrip_MultipleSavePaths_AllFilesRestored()
    {
        using var fixture = new FakeGameFixture();

        var archivePath = await WriteSaveArchiveAsync(fixture.InstallDirectory, fixture.Manifest);
        var destDir     = CreateDestDir();

        RestoreSaveArchive(archivePath, destDir, fixture.Manifest);

        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot1.sav")));
        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot2.sav")));
        Assert.True(File.Exists(Path.Combine(destDir, "config.cfg")));
    }

    [Fact]
    public async Task RoundTrip_OverwritesExistingStaleFile()
    {
        using var fixture = new FakeGameFixture();

        var archivePath = await WriteSaveArchiveAsync(fixture.InstallDirectory, fixture.Manifest);
        var destDir     = CreateDestDir();

        // Pre-populate destination with stale data
        Directory.CreateDirectory(Path.Combine(destDir, "saves"));
        File.WriteAllText(Path.Combine(destDir, "saves", "slot1.sav"), "STALE_CONTENT");

        RestoreSaveArchive(archivePath, destDir, fixture.Manifest);

        Assert.Equal(FakeGameFixture.Slot1Content, File.ReadAllText(Path.Combine(destDir, "saves", "slot1.sav")));
    }

    [Fact]
    public async Task RoundTrip_WithRegexSavePath_OnlyMatchingFilesRestored()
    {
        // Arrange – a game that saves only .sav files via a regex save path
        var sourceDir = CreateDestDir();
        Directory.CreateDirectory(Path.Combine(sourceDir, "saves"));
        File.WriteAllText(Path.Combine(sourceDir, "saves", "slot1.sav"), "SLOT1");
        File.WriteAllText(Path.Combine(sourceDir, "saves", "slot2.sav"), "SLOT2");
        File.WriteAllText(Path.Combine(sourceDir, "saves", "notes.txt"), "NOTES"); // must NOT be packed

        var regexSavePath = new ManifestSavePath
        {
            Id               = Guid.NewGuid(),
            Type             = SavePathType.File,
            Path             = @"\.sav$",
            WorkingDirectory = "{InstallDir}",
            IsRegex          = true
        };

        var manifest = new ManifestGame
        {
            Id        = Guid.NewGuid(),
            Title     = "Regex Save Game",
            SavePaths = new List<ManifestSavePath> { regexSavePath }
        };

        var archivePath = await WriteSaveArchiveAsync(sourceDir, manifest);
        var destDir     = CreateDestDir();

        RestoreSaveArchive(archivePath, destDir, manifest);

        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot1.sav")));
        Assert.True(File.Exists(Path.Combine(destDir, "saves", "slot2.sav")));
        Assert.False(File.Exists(Path.Combine(destDir, "saves", "notes.txt")));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Packs all save paths from <paramref name="manifest"/> and writes the resulting
    /// ZIP archive to a temp file.  Returns the path to that file.
    /// </summary>
    private async Task<string> WriteSaveArchiveAsync(string installDirectory, ManifestGame manifest)
    {
        var stream = await BuildArchiveStreamAsync(installDirectory, manifest);

        var archivePath = Path.Combine(_tempDir, $"save-{Guid.NewGuid()}.zip");
        stream.Position = 0;

        await using var fs = File.Create(archivePath);
        await stream.CopyToAsync(fs);

        return archivePath;
    }

    private static async Task<Stream> BuildArchiveStreamAsync(string installDirectory, ManifestGame manifest)
    {
        using var packer = new SavePacker(installDirectory);

        if (manifest.SavePaths?.Any() == true)
            packer.AddPaths(manifest.SavePaths);

        await packer.AddManifestAsync(manifest);

        var packed = await packer.PackAsync();

        // Copy to a fresh MemoryStream so the caller owns a non-disposed buffer.
        var copy = new MemoryStream();
        packed.Position = 0;
        await packed.CopyToAsync(copy);
        copy.Position = 0;
        return copy;
    }

    /// <summary>
    /// Restores a save archive to <paramref name="installDirectory"/>.
    ///
    /// Mirrors the file-movement block from <c>SaveClient.DownloadAsync</c> exactly,
    /// replacing only the HTTP download step with the pre-built archive file.
    /// This makes the test a faithful integration test of the extraction logic.
    /// </summary>
    private void RestoreSaveArchive(string archivePath, string installDirectory, ManifestGame manifest)
    {
        var tempLocation = Path.Combine(Path.GetTempPath(), $"lc-restore-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempLocation);
            ZipFile.ExtractToDirectory(archivePath, tempLocation, overwriteFiles: true);

            // Mirror legacy-fallback from SaveClient.DownloadAsync
            var tempFilesRoot = Directory.Exists(Path.Combine(tempLocation, "Files")) ? "Files" : "Saves";

            foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == SavePathType.File))
            {
                var entries = _saveClient.GetFileSavePathEntries(savePath, installDirectory) ?? [];

                foreach (var entry in entries)
                {
                    var entryPath = Path.Combine(
                        tempLocation,
                        tempFilesRoot,
                        savePath.Id.ToString(),
                        entry.ArchivePath.Replace('/', Path.DirectorySeparatorChar));

                    var destinationPath = entry.ActualPath.ExpandEnvironmentVariables(installDirectory);

                    if (File.Exists(entryPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        if (File.Exists(destinationPath)) File.Delete(destinationPath);
                        File.Move(entryPath, destinationPath);
                    }
                    else if (Directory.Exists(entryPath))
                    {
                        foreach (var entryFile in Directory.GetFiles(entryPath, "*", SearchOption.AllDirectories))
                        {
                            var fileDestination = entryFile.Replace(entryPath, destinationPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDestination)!);
                            if (File.Exists(fileDestination)) File.Delete(fileDestination);
                            File.Move(entryFile, fileDestination);
                        }
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempLocation))
                Directory.Delete(tempLocation, true);
        }
    }

    /// <summary>Creates a fresh empty directory under _tempDir for use as a restore destination.</summary>
    private string CreateDestDir()
    {
        var dir = Path.Combine(_tempDir, $"dest-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static List<string> EntryKeys(Stream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        return zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
    }
}
