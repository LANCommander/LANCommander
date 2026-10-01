using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Legacy GameType values map to InstallTo/ShowInLibrary the same way the
/// V2_2_0 database migrations do.
/// </summary>
public class GameTypeHelperTests
{
#pragma warning disable CS0618 // Legacy values are what's under test
    [Theory]
    [InlineData(GameType.MainGame, false, GameType.MainGame, GameInstallLocation.OwnDirectory, true)]
    [InlineData(GameType.Expansion, true, GameType.Expansion, GameInstallLocation.BaseGameDirectory, false)]
    [InlineData(GameType.Mod, true, GameType.Mod, GameInstallLocation.BaseGameDirectory, false)]
    [InlineData(GameType.Expansion, false, GameType.Expansion, GameInstallLocation.OwnDirectory, false)]
    [InlineData(GameType.Mod, false, GameType.Mod, GameInstallLocation.OwnDirectory, false)]
    [InlineData(GameType.StandaloneExpansion, true, GameType.Expansion, GameInstallLocation.OwnDirectory, true)]
    [InlineData(GameType.StandaloneExpansion, false, GameType.Expansion, GameInstallLocation.OwnDirectory, true)]
    [InlineData(GameType.StandaloneMod, true, GameType.Mod, GameInstallLocation.BaseGameDirectory, true)]
    [InlineData(GameType.StandaloneMod, false, GameType.Mod, GameInstallLocation.OwnDirectory, true)]
    public void FromLegacyType_MatchesMigration(GameType legacy, bool hasBaseGame, GameType type, GameInstallLocation installTo, bool showInLibrary)
    {
        var resolved = GameTypeHelper.FromLegacyType(legacy, hasBaseGame);

        Assert.Equal(type, resolved.Type);
        Assert.Equal(installTo, resolved.InstallTo);
        Assert.Equal(showInLibrary, resolved.ShowInLibrary);
    }
#pragma warning restore CS0618

    [Fact]
    public void Deserialize_LegacyStandaloneModManifest_IsNormalized()
    {
        // Installed .lancommander manifests written before the fields existed carry only the type name
        var yaml = $"""
            Id: {Guid.NewGuid()}
            Title: Quake Total Conversion
            Type: StandaloneMod
            BaseGame: Quake
            """;

        var manifest = ManifestHelper.Deserialize<ManifestGame>(yaml);

        Assert.Equal(GameType.Mod, manifest.Type);
        Assert.Equal(GameInstallLocation.BaseGameDirectory, manifest.InstallTo);
        Assert.True(manifest.ShowInLibrary);
    }

    [Fact]
    public void Deserialize_LegacyAddonEntries_AreTreatedAsLinkedToTheManifestGame()
    {
        var yaml = $"""
            Id: {Guid.NewGuid()}
            Title: Quake
            Type: MainGame
            Addons:
            - Id: {Guid.NewGuid()}
              Title: Scourge of Armagon
              Type: Expansion
            """;

        var manifest = ManifestHelper.Deserialize<ManifestGame>(yaml);
        var addon = manifest.Addons.Single();

        Assert.Equal(GameInstallLocation.OwnDirectory, manifest.InstallTo);
        Assert.True(manifest.ShowInLibrary);
        Assert.Equal(GameInstallLocation.BaseGameDirectory, addon.InstallTo);
        Assert.False(addon.ShowInLibrary);
    }

    [Fact]
    public void Deserialize_CurrentManifest_KeepsExplicitFields()
    {
        var yaml = $"""
            Id: {Guid.NewGuid()}
            Title: Scourge of Armagon
            Type: Expansion
            BaseGame: Quake
            InstallTo: SubDirectory
            ShowInLibrary: true
            """;

        var manifest = ManifestHelper.Deserialize<ManifestGame>(yaml);

        Assert.Equal(GameType.Expansion, manifest.Type);
        Assert.Equal(GameInstallLocation.SubDirectory, manifest.InstallTo);
        Assert.True(manifest.ShowInLibrary);
    }
}
