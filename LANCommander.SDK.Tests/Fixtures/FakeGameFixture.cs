using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestKey = LANCommander.SDK.Models.Manifest.Key;
using ManifestMedia = LANCommander.SDK.Models.Manifest.Media;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;
using ManifestScript = LANCommander.SDK.Models.Manifest.Script;

namespace LANCommander.SDK.Tests.Fixtures;

/// <summary>
/// Builds a complete fake game installation on disk for integration testing.
/// Creates save files, script stubs, and writes a full manifest covering
/// saves, keys, scripts, and media — everything DownloadAsync / UploadAsync touches.
/// </summary>
public sealed class FakeGameFixture : IDisposable
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Stable ID for the fake game, used as the manifest key and sub-directory name.</summary>
    public Guid GameId { get; } = Guid.NewGuid();

    /// <summary>Root installation directory created by the fixture.</summary>
    public string InstallDirectory { get; }

    // ── Manifest ──────────────────────────────────────────────────────────────

    /// <summary>Fully populated manifest written to <c>.lancommander/{GameId}/Manifest.yml</c>.</summary>
    public ManifestGame Manifest { get; }

    // ── Save paths ────────────────────────────────────────────────────────────

    /// <summary>SavePath that covers the <c>saves/</c> sub-directory inside the install dir.</summary>
    public ManifestSavePath SavesDirSavePath { get; }

    /// <summary>SavePath that covers the single <c>config.cfg</c> file in the install dir root.</summary>
    public ManifestSavePath ConfigFileSavePath { get; }

    // ── Computed on-disk paths ────────────────────────────────────────────────

    public string SavesDirectory    => Path.Combine(InstallDirectory, "saves");
    public string SaveFileSlot1Path => Path.Combine(SavesDirectory, "slot1.sav");
    public string SaveFileSlot2Path => Path.Combine(SavesDirectory, "slot2.sav");
    public string ConfigFilePath    => Path.Combine(InstallDirectory, "config.cfg");

    // ── Known file contents ───────────────────────────────────────────────────

    public const string Slot1Content  = "SAVE_DATA_SLOT_1";
    public const string Slot2Content  = "SAVE_DATA_SLOT_2";
    public const string ConfigContent = "player_name=TestPlayer\ngraphics=high";

    // ── Construction ──────────────────────────────────────────────────────────

    public FakeGameFixture()
    {
        InstallDirectory = Path.Combine(Path.GetTempPath(), $"lc-fake-game-{GameId}");
        Directory.CreateDirectory(InstallDirectory);

        // ── Save files ────────────────────────────────────────────────────────

        Directory.CreateDirectory(SavesDirectory);
        File.WriteAllText(SaveFileSlot1Path, Slot1Content);
        File.WriteAllText(SaveFileSlot2Path, Slot2Content);
        File.WriteAllText(ConfigFilePath,    ConfigContent);

        // ── Script stubs on disk ──────────────────────────────────────────────

        WriteScriptFile(ScriptType.Install,    "Write-Host 'Installing'");
        WriteScriptFile(ScriptType.Uninstall,  "Write-Host 'Uninstalling'");
        WriteScriptFile(ScriptType.BeforeStart,"Write-Host 'Before start'");
        WriteScriptFile(ScriptType.AfterStop,  "Write-Host 'After stop'");
        WriteScriptFile(ScriptType.NameChange, "Write-Host \"Name change: $PlayerAlias\"");
        WriteScriptFile(ScriptType.KeyChange,  "Write-Host \"Key change: $AllocatedKey\"");

        // ── Save paths ────────────────────────────────────────────────────────

        SavesDirSavePath = new ManifestSavePath
        {
            Id             = Guid.NewGuid(),
            Type           = SavePathType.File,
            Path           = "saves",
            WorkingDirectory = "{InstallDir}",
            IsRegex        = false
        };

        ConfigFileSavePath = new ManifestSavePath
        {
            Id             = Guid.NewGuid(),
            Type           = SavePathType.File,
            Path           = "config.cfg",
            WorkingDirectory = "{InstallDir}",
            IsRegex        = false
        };

        // ── Manifest ──────────────────────────────────────────────────────────

        Manifest = new ManifestGame
        {
            Id               = GameId,
            Title            = "Fake Test Game",
            Version          = "1.0.0",
            InstallDirectory = InstallDirectory,

            Keys = new List<ManifestKey>
            {
                new() { Value = "FAKE-KEY-AAAA-1111" },
                new() { Value = "FAKE-KEY-BBBB-2222" }
            },

            Scripts = new List<ManifestScript>
            {
                new() { Id = Guid.NewGuid(), Type = ScriptType.Install,    Name = "Install" },
                new() { Id = Guid.NewGuid(), Type = ScriptType.Uninstall,  Name = "Uninstall" },
                new() { Id = Guid.NewGuid(), Type = ScriptType.BeforeStart,Name = "BeforeStart" },
                new() { Id = Guid.NewGuid(), Type = ScriptType.AfterStop,  Name = "AfterStop" },
                new() { Id = Guid.NewGuid(), Type = ScriptType.NameChange, Name = "NameChange" },
                new() { Id = Guid.NewGuid(), Type = ScriptType.KeyChange,  Name = "KeyChange" }
            },

            Media = new List<ManifestMedia>
            {
                new() { Id = Guid.NewGuid(), FileId = Guid.NewGuid(), Type = MediaType.Cover,      MimeType = "image/jpeg", Crc32 = "AABBCCDD" },
                new() { Id = Guid.NewGuid(), FileId = Guid.NewGuid(), Type = MediaType.Icon,       MimeType = "image/png",  Crc32 = "EEFF0011" },
                new() { Id = Guid.NewGuid(), FileId = Guid.NewGuid(), Type = MediaType.Background, MimeType = "image/jpeg", Crc32 = "22334455" }
            },

            SavePaths = new List<ManifestSavePath>
            {
                SavesDirSavePath,
                ConfigFileSavePath
            }
        };

        ManifestHelper.Write(Manifest, InstallDirectory);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void WriteScriptFile(ScriptType type, string contents)
    {
        var path = ScriptHelper.GetScriptFilePath(InstallDirectory, GameId, type);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(InstallDirectory))
            Directory.Delete(InstallDirectory, true);
    }
}
