using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using SdkGame = LANCommander.SDK.Models.Game;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// <see cref="GameClient.GetInstallDirectory"/> resolves a game's directory from
/// <see cref="SdkGame.InstallTo"/> and <see cref="SdkGame.DirectoryName"/>.
///
/// The base game is modelled as already installed (manifest + .lancommander metadata present),
/// and addons are resolved against the base game's existing directory. That hits the
/// file-system-only branch of GetInstallDirectory, so no server/API is required.
/// </summary>
public class AddonInstallDirectoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _root;
    private readonly Guid _baseGameId = Guid.NewGuid();

    // The base game's resolved install directory ({root}\Quake), already populated with a manifest.
    private readonly string _baseInstallDirectory;

    private readonly GameClient _client = CreateClient();

    public AddonInstallDirectoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-addon-dir-tests-{Guid.NewGuid()}");
        _root = Path.Combine(_tempDir, "Games");
        Directory.CreateDirectory(_root);

        _baseInstallDirectory = Path.Combine(_root, "Quake");
        Directory.CreateDirectory(_baseInstallDirectory);

        // Writing the manifest creates the .lancommander metadata directory that marks an
        // existing installation.
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
    public async Task MainGame_ResolvesToOwnTitleDirectory()
    {
        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Doom: Ultimate", Type = GameType.MainGame };

        var resolved = await _client.GetInstallDirectory(game, _root);

        Assert.Equal(Path.Combine(_root, "Doom - Ultimate"), resolved);
    }

    [Fact]
    public async Task MainGame_DirectoryNameOverridesTitle()
    {
        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Doom", DirectoryName = "DOOM95", Type = GameType.MainGame };

        var resolved = await _client.GetInstallDirectory(game, _root);

        Assert.Equal(Path.Combine(_root, "DOOM95"), resolved);
    }

    [Fact]
    public async Task MainGame_InstalledUnderTitle_KeepsLegacyDirectory()
    {
        // Installed before DirectoryName was honored: the files live under the title, so the
        // existing install must still be found rather than resolving to a new, empty folder.
        var game = new SdkGame { Id = _baseGameId, Title = "Quake", DirectoryName = "id1", Type = GameType.MainGame };

        var resolved = await _client.GetInstallDirectory(game, _root);

        Assert.Equal(_baseInstallDirectory, resolved);
    }

    [Fact]
    public async Task BaseGameDirectory_ExtractsIntoBaseGameDirectory()
    {
        var addon = MakeAddon(GameInstallLocation.BaseGameDirectory);

        var resolved = await _client.GetInstallDirectory(addon, _baseInstallDirectory);

        Assert.Equal(_baseInstallDirectory, resolved);
    }

    [Fact]
    public async Task BaseGameDirectory_IgnoresDirectoryName()
    {
        var addon = MakeAddon(GameInstallLocation.BaseGameDirectory, directoryName: "hipnotic");

        var resolved = await _client.GetInstallDirectory(addon, _baseInstallDirectory);

        Assert.Equal(_baseInstallDirectory, resolved);
    }

    [Fact]
    public async Task SubDirectory_UsesTitleInsideBaseGameDirectory()
    {
        var addon = MakeAddon(GameInstallLocation.SubDirectory);

        var resolved = await _client.GetInstallDirectory(addon, _baseInstallDirectory);

        Assert.Equal(Path.Combine(_baseInstallDirectory, "Scourge of Armagon"), resolved);
    }

    [Fact]
    public async Task SubDirectory_UsesDirectoryNameInsideBaseGameDirectory()
    {
        var addon = MakeAddon(GameInstallLocation.SubDirectory, directoryName: "hipnotic");

        var resolved = await _client.GetInstallDirectory(addon, _baseInstallDirectory);

        Assert.Equal(Path.Combine(_baseInstallDirectory, "hipnotic"), resolved);
    }

    [Fact]
    public async Task OwnDirectory_ResolvesUnderInstallRoot()
    {
        var addon = MakeAddon(GameInstallLocation.OwnDirectory);

        var resolved = await _client.GetInstallDirectory(addon, _root);

        Assert.Equal(Path.Combine(_root, "Scourge of Armagon"), resolved);
    }

    [Fact]
    public async Task OwnDirectory_WhenGivenBaseGameDirectory_ResolvesBesideIt()
    {
        // Modifying an installation passes the base game's directory instead of the root
        var addon = MakeAddon(GameInstallLocation.OwnDirectory, directoryName: "Armagon");

        var resolved = await _client.GetInstallDirectory(addon, _baseInstallDirectory);

        Assert.Equal(Path.Combine(_root, "Armagon"), resolved);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(GameInstallLocation.BaseGameDirectory, "")]
    [InlineData(GameInstallLocation.SubDirectory, "hipnotic")]
    [InlineData(GameInstallLocation.OwnDirectory, "../hipnotic")]
    public void GetAddonInstallDirectory_ResolvesRelativeToBaseGame(GameInstallLocation? installTo, string expectedRelative)
    {
        var addon = new ManifestGame { Id = Guid.NewGuid(), Title = "Scourge of Armagon", DirectoryName = "hipnotic", InstallTo = installTo };

        var resolved = GameClient.GetAddonInstallDirectory(_baseInstallDirectory, addon);

        Assert.Equal(Path.GetFullPath(Path.Combine(_baseInstallDirectory, expectedRelative)), Path.GetFullPath(resolved));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(GameInstallLocation.BaseGameDirectory, "")]
    [InlineData(GameInstallLocation.SubDirectory, "hipnotic")]
    [InlineData(GameInstallLocation.OwnDirectory, "../hipnotic")]
    public void GetBaseInstallDirectory_FindsBaseGameFromAddonDirectory(GameInstallLocation? installTo, string addonRelative)
    {
        var addon = new ManifestGame { Id = Guid.NewGuid(), Title = "Scourge of Armagon", Type = GameType.Expansion, InstallTo = installTo, BaseGameId = _baseGameId };
        var addonDirectory = Path.GetFullPath(Path.Combine(_baseInstallDirectory, addonRelative));

        Directory.CreateDirectory(addonDirectory);
        ManifestHelper.Write(addon, addonDirectory);

        // A sibling game that isn't the base game must not be picked up
        var otherDirectory = Path.Combine(_root, "Doom");
        Directory.CreateDirectory(otherDirectory);
        ManifestHelper.Write(new ManifestGame { Id = Guid.NewGuid(), Title = "Doom", Type = GameType.MainGame }, otherDirectory);

        var resolved = GameClient.GetBaseInstallDirectory(addonDirectory, addon);

        Assert.Equal(Path.GetFullPath(_baseInstallDirectory), Path.GetFullPath(resolved));
    }

    [Fact]
    public void GetBaseInstallDirectory_MainGame_ReturnsOwnDirectory()
    {
        var game = new ManifestGame { Id = _baseGameId, Title = "Quake", Type = GameType.MainGame };

        Assert.Equal(_baseInstallDirectory, GameClient.GetBaseInstallDirectory(_baseInstallDirectory, game));
    }

    [Fact]
    public void GetBaseInstallDirectory_BaseGameMissing_ReturnsNull()
    {
        var addon = new ManifestGame { Id = Guid.NewGuid(), Title = "Orphan", Type = GameType.Mod, InstallTo = GameInstallLocation.OwnDirectory, BaseGameId = Guid.NewGuid() };
        var addonDirectory = Path.Combine(_root, "Orphan");
        Directory.CreateDirectory(addonDirectory);

        Assert.Null(GameClient.GetBaseInstallDirectory(addonDirectory, addon));
    }

    private SdkGame MakeAddon(GameInstallLocation installTo, string? directoryName = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Scourge of Armagon",
        DirectoryName = directoryName,
        Type = GameType.Expansion,
        InstallTo = installTo,
        BaseGameId = _baseGameId,
    };

    /// <summary>
    /// Builds a GameClient whose dependencies are unused for the file-system-only directory
    /// resolution exercised here, so they are safe to leave null.
    /// </summary>
    private static GameClient CreateClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
}
