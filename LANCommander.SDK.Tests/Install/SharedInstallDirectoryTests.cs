using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestTool = LANCommander.SDK.Models.Manifest.Tool;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Addons and tools install into the base game's directory rather than one of their own. Uninstalling
/// one used to fall back to deleting that whole directory when it had no file list, which took the
/// base game and every other addon with it.
/// </summary>
public class SharedInstallDirectoryTests : IDisposable
{
    private readonly string _installDirectory;

    public SharedInstallDirectoryTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-shared-dir-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    private async Task<Guid> InstallGameAsync(GameType type = GameType.MainGame)
    {
        var id = Guid.NewGuid();

        await ManifestHelper.WriteAsync(new ManifestGame
        {
            Id = id,
            Title = $"Game {id:N}",
            Type = type,
            Version = "1.0.0",
        }, _installDirectory);

        return id;
    }

    private async Task<Guid> InstallToolAsync()
    {
        var id = Guid.NewGuid();

        await ManifestHelper.WriteAsync(new ManifestTool { Id = id, Name = $"Tool {id:N}" }, _installDirectory);

        return id;
    }

    [Fact]
    public async Task ADirectoryHoldingOnlyTheEntityItselfIsNotShared()
    {
        var gameId = await InstallGameAsync();

        Assert.False(GameClient.IsInstallDirectoryShared(_installDirectory, gameId));
    }

    /// <summary>The base game is still installed, so the addon must not delete the directory.</summary>
    [Fact]
    public async Task ADirectoryHoldingTheBaseGameIsSharedFromTheAddonsPerspective()
    {
        var baseGameId = await InstallGameAsync();
        var addonId = await InstallGameAsync(GameType.Expansion);

        Assert.True(GameClient.IsInstallDirectoryShared(_installDirectory, addonId));
        Assert.True(GameClient.IsInstallDirectoryShared(_installDirectory, baseGameId));
    }

    [Fact]
    public async Task ADirectoryHoldingAnotherAddonIsShared()
    {
        await InstallGameAsync(GameType.Mod);
        var secondAddonId = await InstallGameAsync(GameType.Mod);

        Assert.True(GameClient.IsInstallDirectoryShared(_installDirectory, secondAddonId));
    }

    /// <summary>A tool installed alongside the game counts too.</summary>
    [Fact]
    public async Task ADirectoryHoldingAToolIsShared()
    {
        var gameId = await InstallGameAsync();
        await InstallToolAsync();

        Assert.True(GameClient.IsInstallDirectoryShared(_installDirectory, gameId));
    }

    [Fact]
    public void ADirectoryWithNoMetadataIsNotShared()
    {
        Assert.False(GameClient.IsInstallDirectoryShared(_installDirectory, Guid.NewGuid()));
    }

    /// <summary>
    /// A metadata folder with no manifest is leftover state, not an install, so it must not pin the
    /// directory as shared forever.
    /// </summary>
    [Fact]
    public void AMetadataFolderWithoutAManifestIsNotShared()
    {
        Directory.CreateDirectory(Path.Combine(_installDirectory, ".lancommander", Guid.NewGuid().ToString()));

        Assert.False(GameClient.IsInstallDirectoryShared(_installDirectory, Guid.NewGuid()));
    }

    [Fact]
    public void ANonGuidMetadataFolderIsIgnored()
    {
        Directory.CreateDirectory(Path.Combine(_installDirectory, ".lancommander", "not-a-guid"));

        Assert.False(GameClient.IsInstallDirectoryShared(_installDirectory, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyPathIsNotShared(string? installDirectory)
    {
        Assert.False(GameClient.IsInstallDirectoryShared(installDirectory!, Guid.NewGuid()));
    }
}
