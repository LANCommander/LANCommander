using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using SdkGame = LANCommander.SDK.Models.Game;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// A StandaloneMod is presented as a separate game in the library, but its archive extracts
/// into the base game's install directory (an overlay). These tests lock in that
/// <see cref="GameClient.GetInstallDirectory"/> resolves a StandaloneMod to the base game's
/// existing directory rather than a private "\Title" subfolder.
///
/// The base game is modelled as already installed (manifest + .lancommander metadata present),
/// which is the state after "installing a standalone mod triggers the base game install". This
/// hits the file-system-only detection branch of GetInstallDirectory, so no server/API is required.
/// </summary>
public class StandaloneModInstallDirectoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _root;
    private readonly Guid _baseGameId = Guid.NewGuid();

    // The base game's resolved install directory ({root}\Quake), already populated with a manifest.
    private readonly string _baseInstallDirectory;

    private readonly GameClient _client = CreateClient();

    public StandaloneModInstallDirectoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-standalone-mod-tests-{Guid.NewGuid()}");
        _root = Path.Combine(_tempDir, "Games");
        Directory.CreateDirectory(_root);

        _baseInstallDirectory = Path.Combine(_root, "Quake");
        Directory.CreateDirectory(_baseInstallDirectory);

        // Base game is already installed — writing the manifest creates the .lancommander
        // metadata directory that marks an existing installation.
        ManifestHelper.Write(
            new ManifestGame
            {
                Id = _baseGameId,
                Title = "Quake",
                Type = GameType.MainGame,
                Version = "1.0.0",
            },
            _baseInstallDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task StandaloneMod_ExtractsIntoBaseGameDirectory()
    {
        var mod = MakeStandaloneMod();

        // The base game directory already exists (with .lancommander metadata), so the mod
        // overlays it rather than creating its own "{baseDir}\Quake Total Conversion" subfolder.
        var resolved = await _client.GetInstallDirectory(mod, _baseInstallDirectory);

        Assert.Equal(_baseInstallDirectory, resolved);
    }

    [Fact]
    public async Task StandaloneMod_DoesNotCreateOwnTitleSubdirectory()
    {
        var mod = MakeStandaloneMod();

        var resolved = await _client.GetInstallDirectory(mod, _baseInstallDirectory);

        // Regression guard: if the StandaloneMod were treated like a standalone/main game it
        // would resolve to a private subfolder named after the mod. It must not.
        Assert.NotEqual(Path.Combine(_baseInstallDirectory, mod.Title), resolved);
    }

    [Fact]
    public async Task MainGame_ResolvesToOwnTitleSubdirectory()
    {
        // Contrast: a non-overlay game resolves to its own "{root}\Title" directory.
        var game = new SdkGame
        {
            Id = Guid.NewGuid(),
            Title = "Doom",
            Type = GameType.MainGame,
            BaseGameId = Guid.Empty,
        };

        var resolved = await _client.GetInstallDirectory(game, _root);

        Assert.Equal(Path.Combine(_root, "Doom"), resolved);
    }

    private SdkGame MakeStandaloneMod() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Quake Total Conversion",
        Type = GameType.StandaloneMod,
        BaseGameId = _baseGameId,
    };

    /// <summary>
    /// Builds a GameClient whose dependencies are unused for the file-system-only directory
    /// resolution exercised here, so they are safe to leave null.
    /// </summary>
    private static GameClient CreateClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
}
