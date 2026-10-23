using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using LANCommander.SDK.Utilities;
using Microsoft.Extensions.Logging;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;

namespace LANCommander.SDK.Tests.Utilities;

/// <summary>
/// Packs save files from one install directory, then restores the archive into another and
/// checks that every file comes back with the right contents in the right place. These cover
/// the save path shapes the server UI produces: plain files, directories, nested paths with
/// either separator, {InstallDir}-prefixed paths, working directories outside the install
/// directory, and regex patterns.
/// </summary>
public class SaveRoundTripTests : IDisposable
{
    private readonly string _root;
    private readonly string _sourceInstall;
    private readonly string _targetInstall;
    private readonly RecordingLogger<SaveClient> _logger = new();
    private readonly SaveClient _saveClient;

    public SaveRoundTripTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"lc-saveroundtrip-{Guid.NewGuid()}");
        _sourceInstall = Path.Combine(_root, "source", "Game");
        _targetInstall = Path.Combine(_root, "target", "Game");

        Directory.CreateDirectory(_sourceInstall);
        Directory.CreateDirectory(_targetInstall);

        // Only the pack/restore paths are exercised; no API calls or registry imports happen.
        _saveClient = new SaveClient(null!, null!, null!, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static ManifestSavePath FileSavePath(string path, string? workingDirectory = "{InstallDir}", bool isRegex = false) => new()
    {
        Id = Guid.NewGuid(),
        Type = SavePathType.File,
        Path = path,
        WorkingDirectory = workingDirectory!,
        IsRegex = isRegex,
    };

    private static void WriteFile(string directory, string relativePath, string content)
    {
        var full = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static string? ReadFile(string directory, string relativePath)
    {
        var full = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    private static ManifestGame Manifest(params ManifestSavePath[] savePaths) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Round Trip",
        SavePaths = savePaths,
    };

    private async Task<string> PackToFileAsync(string installDirectory, ManifestGame manifest)
    {
        using var packer = new SavePacker(installDirectory);

        packer.AddPaths(manifest.SavePaths);

        Assert.True(packer.HasEntries(), "No save files were packed");

        await packer.AddManifestAsync(manifest);

        var stream = await packer.PackAsync();
        var archivePath = Path.Combine(_root, $"{Guid.NewGuid()}.zip");

        await using (var file = File.Create(archivePath))
            await stream.CopyToAsync(file);

        return archivePath;
    }

    private async Task RoundTripAsync(ManifestGame manifest, string installDirectory, string restoreDirectory)
    {
        var archivePath = await PackToFileAsync(installDirectory, manifest);

        await _saveClient.RestoreAsync(archivePath, restoreDirectory, manifest.Id);

        Assert.Empty(_logger.Errors);
    }

    // ── Non-regex ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("save.dat", "save.dat")]
    [InlineData("baseq3/autoexec.cfg", "baseq3/autoexec.cfg")]
    [InlineData("baseq3\\autoexec.cfg", "baseq3/autoexec.cfg")]
    [InlineData("{InstallDir}/baseq3/autoexec.cfg", "baseq3/autoexec.cfg")]
    [InlineData("{InstallDir}\\baseq3\\autoexec.cfg", "baseq3/autoexec.cfg")]
    public async Task SingleFile_RestoresToFreshInstall(string savePathPath, string fileOnDisk)
    {
        WriteFile(_sourceInstall, fileOnDisk, "slot-1");

        await RoundTripAsync(Manifest(FileSavePath(savePathPath)), _sourceInstall, _targetInstall);

        Assert.Equal("slot-1", ReadFile(_targetInstall, fileOnDisk));
    }

    [Theory]
    [InlineData("saves")]
    [InlineData("saves/")]
    [InlineData("{InstallDir}/saves")]
    [InlineData("{InstallDir}\\saves\\")]
    public async Task Directory_RestoresAllFilesToFreshInstall(string savePathPath)
    {
        WriteFile(_sourceInstall, "saves/slot1.sav", "one");
        WriteFile(_sourceInstall, "saves/profiles/player.cfg", "player");

        await RoundTripAsync(Manifest(FileSavePath(savePathPath)), _sourceInstall, _targetInstall);

        Assert.Equal("one", ReadFile(_targetInstall, "saves/slot1.sav"));
        Assert.Equal("player", ReadFile(_targetInstall, "saves/profiles/player.cfg"));
    }

    [Fact]
    public async Task SingleFile_OverwritesExistingFileOnRestore()
    {
        WriteFile(_sourceInstall, "save.dat", "new");
        WriteFile(_targetInstall, "save.dat", "old");

        await RoundTripAsync(Manifest(FileSavePath("save.dat")), _sourceInstall, _targetInstall);

        Assert.Equal("new", ReadFile(_targetInstall, "save.dat"));
    }

    [Fact]
    public async Task SingleFile_RestoresIntoSameInstallAfterDeletion()
    {
        WriteFile(_sourceInstall, "save.dat", "kept");
        var manifest = Manifest(FileSavePath("save.dat"));
        var archivePath = await PackToFileAsync(_sourceInstall, manifest);

        File.Delete(Path.Combine(_sourceInstall, "save.dat"));

        await _saveClient.RestoreAsync(archivePath, _sourceInstall, manifest.Id);

        Assert.Empty(_logger.Errors);
        Assert.Equal("kept", ReadFile(_sourceInstall, "save.dat"));
    }

    [Fact]
    public async Task SingleFile_WithSubdirectoryWorkingDirectory_Restores()
    {
        WriteFile(_sourceInstall, "baseq3/autoexec.cfg", "bind w +forward");

        await RoundTripAsync(Manifest(FileSavePath("autoexec.cfg", "{InstallDir}/baseq3")), _sourceInstall, _targetInstall);

        Assert.Equal("bind w +forward", ReadFile(_targetInstall, "baseq3/autoexec.cfg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SingleFile_WithMissingWorkingDirectory_UsesInstallDirectory(string? workingDirectory)
    {
        // Older save paths (and ones where the field was cleared) have no working directory
        WriteFile(_sourceInstall, "save.dat", "data");

        await RoundTripAsync(Manifest(FileSavePath("save.dat", workingDirectory)), _sourceInstall, _targetInstall);

        Assert.Equal("data", ReadFile(_targetInstall, "save.dat"));
    }

    // Mirrors %APPDATA%\Game style working directories without touching the real profile folders.
    // The variable name is unique per test so parallel tests don't collide.
    private string ExternalDirectoryVariable(out string external)
    {
        var variable = $"LC_SAVE_TEST_{Guid.NewGuid():N}";
        external = Path.Combine(_root, "external", "Game");

        Environment.SetEnvironmentVariable(variable, external);

        return variable;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkingDirectory_OutsideInstallDirectory_ViaEnvironmentVariable_Restores(bool isRegex)
    {
        var variable = ExternalDirectoryVariable(out var external);

        try
        {
            WriteFile(external, "Profiles/Player1/progress.sav", "level 5");

            var manifest = Manifest(FileSavePath(isRegex ? @"^Profiles/.*\.sav$" : "Profiles", $"%{variable}%", isRegex));

            var archivePath = await PackToFileAsync(_sourceInstall, manifest);

            Directory.Delete(external, true);

            await _saveClient.RestoreAsync(archivePath, _targetInstall, manifest.Id);

            Assert.Empty(_logger.Errors);
            Assert.Equal("level 5", ReadFile(external, "Profiles/Player1/progress.sav"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task SingleFile_OutsideWorkingDirectory_RestoresToFullPath()
    {
        // Path is absolute (via a variable) while the working directory is left at the default
        var variable = ExternalDirectoryVariable(out var external);

        try
        {
            WriteFile(external, "settings.cfg", "fov 90");

            var manifest = Manifest(FileSavePath($"%{variable}%/settings.cfg"));
            var archivePath = await PackToFileAsync(_sourceInstall, manifest);

            File.Delete(Path.Combine(external, "settings.cfg"));

            await _saveClient.RestoreAsync(archivePath, _targetInstall, manifest.Id);

            Assert.Empty(_logger.Errors);
            Assert.Equal("fov 90", ReadFile(external, "settings.cfg"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task Directory_SameAsWorkingDirectory_Restores()
    {
        WriteFile(_sourceInstall, "saves/slot1.sav", "one");

        await RoundTripAsync(Manifest(FileSavePath("{InstallDir}/saves", "{InstallDir}/saves")), _sourceInstall, _targetInstall);

        Assert.Equal("one", ReadFile(_targetInstall, "saves/slot1.sav"));
    }

    [Fact]
    public async Task Directory_SiblingSharingWorkingDirectoryPrefix_KeepsItsFolder()
    {
        // "{InstallDir}/save" is a string prefix of "{InstallDir}/saves" but not its parent
        WriteFile(_sourceInstall, "saves/slot1.sav", "one");

        await RoundTripAsync(Manifest(FileSavePath("{InstallDir}/saves", "{InstallDir}/save")), _sourceInstall, _targetInstall);

        Assert.Equal("one", ReadFile(_targetInstall, "saves/slot1.sav"));
    }

    [Fact]
    public async Task MultipleSavePaths_AllRestore()
    {
        WriteFile(_sourceInstall, "save.dat", "save");
        WriteFile(_sourceInstall, "config/settings.ini", "settings");

        await RoundTripAsync(Manifest(FileSavePath("save.dat"), FileSavePath("config")), _sourceInstall, _targetInstall);

        Assert.Equal("save", ReadFile(_targetInstall, "save.dat"));
        Assert.Equal("settings", ReadFile(_targetInstall, "config/settings.ini"));
    }

    [Fact]
    public async Task FileContents_ArePackedNotJustEntryNames()
    {
        var payload = new string('x', 64 * 1024);
        WriteFile(_sourceInstall, "big.sav", payload);

        await RoundTripAsync(Manifest(FileSavePath("big.sav")), _sourceInstall, _targetInstall);

        Assert.Equal(payload, ReadFile(_targetInstall, "big.sav"));
    }

    // ── Regex ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(@".*\.sav$")]
    [InlineData(@"\.sav$")]
    [InlineData(@"^saves/.*\.sav$")]
    public async Task Regex_RestoresMatchingFilesToFreshInstall(string pattern)
    {
        WriteFile(_sourceInstall, "saves/slot1.sav", "one");
        WriteFile(_sourceInstall, "saves/slot2.sav", "two");
        WriteFile(_sourceInstall, "saves/readme.txt", "ignored");

        await RoundTripAsync(Manifest(FileSavePath(pattern, isRegex: true)), _sourceInstall, _targetInstall);

        Assert.Equal("one", ReadFile(_targetInstall, "saves/slot1.sav"));
        Assert.Equal("two", ReadFile(_targetInstall, "saves/slot2.sav"));
        Assert.Null(ReadFile(_targetInstall, "saves/readme.txt"));
    }

    [Fact]
    public async Task Regex_RestoresIntoSameInstallAfterDeletion()
    {
        WriteFile(_sourceInstall, "slot1.sav", "one");
        var manifest = Manifest(FileSavePath(@"\.sav$", isRegex: true));
        var archivePath = await PackToFileAsync(_sourceInstall, manifest);

        File.Delete(Path.Combine(_sourceInstall, "slot1.sav"));

        await _saveClient.RestoreAsync(archivePath, _sourceInstall, manifest.Id);

        Assert.Empty(_logger.Errors);
        Assert.Equal("one", ReadFile(_sourceInstall, "slot1.sav"));
    }

    [Fact]
    public async Task Regex_OverwritesExistingFilesOnRestore()
    {
        WriteFile(_sourceInstall, "slot1.sav", "new");
        WriteFile(_targetInstall, "slot1.sav", "old");

        await RoundTripAsync(Manifest(FileSavePath(@"\.sav$", isRegex: true)), _sourceInstall, _targetInstall);

        Assert.Equal("new", ReadFile(_targetInstall, "slot1.sav"));
    }

    [Fact]
    public async Task Regex_WithSubdirectoryWorkingDirectory_Restores()
    {
        WriteFile(_sourceInstall, "baseq3/autoexec.cfg", "exec");
        WriteFile(_sourceInstall, "baseq3/q3config.cfg", "config");
        WriteFile(_sourceInstall, "baseq3/pak0.pk3", "pak");

        await RoundTripAsync(Manifest(FileSavePath(@"\.cfg$", "{InstallDir}/baseq3", isRegex: true)), _sourceInstall, _targetInstall);

        Assert.Equal("exec", ReadFile(_targetInstall, "baseq3/autoexec.cfg"));
        Assert.Equal("config", ReadFile(_targetInstall, "baseq3/q3config.cfg"));
        Assert.Null(ReadFile(_targetInstall, "baseq3/pak0.pk3"));
    }

    [Fact]
    public async Task Regex_WithTrailingSeparatorInstallDirectory_Restores()
    {
        WriteFile(_sourceInstall, "saves/slot1.sav", "one");

        await RoundTripAsync(
            Manifest(FileSavePath(@"^saves/.*\.sav$", isRegex: true)),
            _sourceInstall + Path.DirectorySeparatorChar,
            _targetInstall + Path.DirectorySeparatorChar);

        Assert.Equal("one", ReadFile(_targetInstall, "saves/slot1.sav"));
    }

    // ── SaveClient.PackAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveClientPackAsync_ReturnsReadableStream()
    {
        WriteFile(_sourceInstall, "save.dat", "data");
        var manifest = Manifest(FileSavePath("save.dat"));

        var stream = await _saveClient.PackAsync(_sourceInstall, manifest);

        Assert.True(stream.CanRead, "Stream returned from PackAsync has been disposed");
        Assert.True(stream.Length > 0);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Errors { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                Errors.Add($"{formatter(state, exception)} {exception}");
        }
    }
}
